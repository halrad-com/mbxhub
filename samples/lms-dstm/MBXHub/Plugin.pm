package Plugins::MBXHub::Plugin;

# MBXHub for Lyrion Music Server — browse / search / stream a MusicBee
# library through MBXHub, plus a "Don't Stop the Music" provider backed by
# the Hub's saved AutoQ stations (read-only: pick one, never edit them).
#
# Deliberate boundary: this plugin never controls MusicBee playback and
# never mirrors LMS transport state back to the Hub. It reads library data,
# streams audio, and asks AutoQ for picks. Nothing else.
#
# Skeleton conventions cribbed from open-source LMS plugins (OPMLBased feed
# plugins like Qobuz/LastMix): install.xml manifest, strings.txt tokens,
# Settings.pm + HTML template, DSTM handler registered in postinitPlugin.

use strict;
use warnings;

use base qw(Slim::Plugin::OPMLBased);

use URI::Escape qw(uri_escape_utf8);

use Slim::Utils::Log;
use Slim::Utils::Prefs;
use Slim::Utils::Strings qw(cstring);

use Plugins::MBXHub::API;

my $log = Slim::Utils::Log->addLogCategory({
	category     => 'plugin.mbxhub',
	defaultLevel => 'WARN',
	description  => 'PLUGIN_MBXHUB',
});

my $prefs = preferences('plugin.mbxhub');

# How many list rows we ask the Hub for in one go. The Hub caps limits
# server-side (10k on lookups); a basic plugin doesn't paginate.
use constant MAX_ARTISTS   => 10000;
use constant MAX_ALBUMS    => 5000;
use constant MAX_TRACKS    => 1000;
use constant SEARCH_LIMIT  => 100;
use constant DSTM_COUNT    => 10;

sub initPlugin {
	my $class = shift;

	$prefs->init({
		host     => '127.0.0.1',
		port     => 8080,
		username => '',
		password => '',
	});

	$prefs->setValidate({ validator => 'intlimit', low => 1, high => 65535 }, 'port');

	$class->SUPER::initPlugin(
		feed   => \&handleFeed,
		tag    => 'mbxhub',
		menu   => 'browse',
		is_app => 1,
	);

	if ( main::WEBUI ) {
		require Plugins::MBXHub::Settings;
		Plugins::MBXHub::Settings->new;
	}
}

sub postinitPlugin {
	my $class = shift;

	# Register the DSTM provider only when the Don't Stop the Music plugin
	# is actually enabled — same guard every DSTM-capable plugin uses.
	if ( Slim::Utils::PluginManager->isEnabled('Slim::Plugin::DontStopTheMusic::Plugin') ) {
		require Slim::Plugin::DontStopTheMusic::Plugin;
		Slim::Plugin::DontStopTheMusic::Plugin->registerHandler(
			'PLUGIN_MBXHUB_DSTM_AUTOQ', \&dontStopTheMusic
		);
	}
}

sub getDisplayName { 'PLUGIN_MBXHUB' }

# ---------------------------------------------------------------------------
# Top-level menu
# ---------------------------------------------------------------------------

sub handleFeed {
	my ($client, $cb, $args) = @_;

	$cb->({
		items => [
			{
				name => cstring($client, 'PLUGIN_MBXHUB_ALBUMS'),
				type => 'link',
				url  => \&albumsFeed,
			},
			{
				name => cstring($client, 'PLUGIN_MBXHUB_ARTISTS'),
				type => 'link',
				url  => \&artistsFeed,
			},
			{
				name => cstring($client, 'PLUGIN_MBXHUB_SEARCH'),
				type => 'search',
				url  => \&searchFeed,
			},
			{
				name => cstring($client, 'PLUGIN_MBXHUB_PLAYLISTS'),
				type => 'link',
				url  => \&playlistsFeed,
			},
			{
				name => cstring($client, 'PLUGIN_MBXHUB_STATIONS'),
				type => 'link',
				url  => \&stationsFeed,
			},
		],
	});
}

# ---------------------------------------------------------------------------
# Albums — GET /library/albums/detailed (one call, artwork via firstTrackUrl)
# ---------------------------------------------------------------------------

sub albumsFeed {
	my ($client, $cb, $args) = @_;

	Plugins::MBXHub::API->get('/library/albums/detailed?limit=' . MAX_ALBUMS, sub {
		my $data = shift;

		my @items;
		for my $album ( @{ ($data && $data->{albums}) || [] } ) {
			next unless defined $album->{name} && $album->{name} ne '';

			push @items, {
				name  => $album->{name},
				line1 => $album->{name},
				line2 => $album->{albumArtist} || '',
				type  => 'playlist',
				url   => \&albumTracksFeed,
				passthrough => [ {
					album       => $album->{name},
					albumArtist => $album->{albumArtist},
				} ],
				$album->{firstTrackUrl}
					? ( image => Plugins::MBXHub::API->artworkUrl($album->{firstTrackUrl}) )
					: (),
			};
		}

		$cb->({ items => _orEmpty($client, \@items) });
	});
}

# Tracks of one album. Filters by album + albumArtist (or artist when we came
# in through the Artists drilldown); falls back to album-only when the
# combined filter matches nothing (compilations, split credits).
sub albumTracksFeed {
	my ($client, $cb, $args, $pass) = @_;

	my $qs = '/library/files?sort=track&limit=' . MAX_TRACKS
		. '&album=' . uri_escape_utf8($pass->{album} // '');

	if ( defined $pass->{albumArtist} && $pass->{albumArtist} ne '' ) {
		$qs .= '&albumArtist=' . uri_escape_utf8($pass->{albumArtist});
	}
	elsif ( defined $pass->{artist} && $pass->{artist} ne '' ) {
		$qs .= '&artist=' . uri_escape_utf8($pass->{artist});
	}

	Plugins::MBXHub::API->get($qs, sub {
		my $data = shift;
		my @items = map { _trackItem($_) } @{ ($data && $data->{tracks}) || [] };

		if ( !@items && ($pass->{albumArtist} || $pass->{artist}) ) {
			# Retry unfiltered by artist — album name alone.
			my $retry = '/library/files?sort=track&limit=' . MAX_TRACKS
				. '&album=' . uri_escape_utf8($pass->{album} // '');

			return Plugins::MBXHub::API->get($retry, sub {
				my $data2 = shift;
				my @retryItems = map { _trackItem($_) } @{ ($data2 && $data2->{tracks}) || [] };
				$cb->({ items => _orEmpty($client, \@retryItems) });
			});
		}

		$cb->({ items => _orEmpty($client, \@items) });
	});
}

# ---------------------------------------------------------------------------
# Artists — GET /library/artists, drill into albums via /library/albums/by-artist
# ---------------------------------------------------------------------------

sub artistsFeed {
	my ($client, $cb, $args) = @_;

	Plugins::MBXHub::API->get('/library/artists?limit=' . MAX_ARTISTS, sub {
		my $data = shift;

		my @items;
		for my $artist ( @{ ($data && $data->{artists}) || [] } ) {
			next unless defined $artist->{name} && $artist->{name} ne '';

			push @items, {
				name => $artist->{name},
				type => 'link',
				url  => \&artistAlbumsFeed,
				passthrough => [ { artist => $artist->{name} } ],
			};
		}

		$cb->({ items => _orEmpty($client, \@items) });
	});
}

sub artistAlbumsFeed {
	my ($client, $cb, $args, $pass) = @_;

	# ?artist= queries ArtistPeople (broader than exact AlbumArtist credit),
	# so featured/guest appearances surface too.
	my $qs = '/library/albums/by-artist?sort=year&artist=' . uri_escape_utf8($pass->{artist} // '');

	Plugins::MBXHub::API->get($qs, sub {
		my $data = shift;

		my @items;
		for my $album ( @{ ($data && $data->{albums}) || [] } ) {
			next unless defined $album->{name} && $album->{name} ne '';

			push @items, {
				name  => $album->{name} . ( $album->{year} ? " ($album->{year})" : '' ),
				line1 => $album->{name},
				line2 => $album->{year} || '',
				type  => 'playlist',
				url   => \&albumTracksFeed,
				passthrough => [ {
					album  => $album->{name},
					artist => $pass->{artist},
				} ],
				$album->{firstTrackUrl}
					? ( image => Plugins::MBXHub::API->artworkUrl($album->{firstTrackUrl}) )
					: (),
			};
		}

		$cb->({ items => _orEmpty($client, \@items) });
	});
}

# ---------------------------------------------------------------------------
# Search — GET /search?q=…&types=tracks&dsl=true (full query DSL passes through)
# ---------------------------------------------------------------------------

sub searchFeed {
	my ($client, $cb, $args) = @_;

	my $query = $args->{search};

	if ( !defined $query || $query !~ /\S/ ) {
		return $cb->({ items => [] });
	}

	my $qs = '/search?types=tracks&dsl=true&limit=' . SEARCH_LIMIT
		. '&q=' . uri_escape_utf8($query);

	Plugins::MBXHub::API->get($qs, sub {
		my $data = shift;

		my $bucket = ($data && ref $data->{buckets} eq 'HASH') ? $data->{buckets}{tracks} : undef;
		my @items  = map { _trackItem($_) } @{ ($bucket && $bucket->{items}) || [] };

		$cb->({ items => _orEmpty($client, \@items) });
	});
}

# ---------------------------------------------------------------------------
# Playlists — GET /playlists, tracks via GET /playlists/{url}/files.
# Journeys saved as playlists in MBXHub show up here with zero extra code.
# ---------------------------------------------------------------------------

sub playlistsFeed {
	my ($client, $cb, $args) = @_;

	Plugins::MBXHub::API->get('/playlists', sub {
		my $data = shift;

		my @items;
		for my $pl ( @{ ($data && $data->{playlists}) || [] } ) {
			next unless defined $pl->{url} && $pl->{url} ne '';

			push @items, {
				name  => ($pl->{name} || $pl->{url}),
				line1 => ($pl->{name} || $pl->{url}),
				line2 => defined $pl->{trackCount} ? "$pl->{trackCount} tracks" : '',
				type  => 'playlist',
				url   => \&playlistTracksFeed,
				passthrough => [ { url => $pl->{url} } ],
			};
		}

		$cb->({ items => _orEmpty($client, \@items) });
	});
}

sub playlistTracksFeed {
	my ($client, $cb, $args, $pass) = @_;

	my $qs = '/playlists/' . uri_escape_utf8($pass->{url} // '') . '/files?limit=' . MAX_TRACKS;

	Plugins::MBXHub::API->get($qs, sub {
		my $data = shift;
		my @items = map { _trackItem($_) } @{ ($data && $data->{tracks}) || [] };
		$cb->({ items => _orEmpty($client, \@items) });
	});
}

# ---------------------------------------------------------------------------
# Stations — GET /autoq/stations, strictly read-only. Picking one stores the
# station id per player; the DSTM handler consumes it. No create / rename /
# delete from here, ever — stations are built and trained in MBXHub.
# ---------------------------------------------------------------------------

sub stationsFeed {
	my ($client, $cb, $args) = @_;

	Plugins::MBXHub::API->get('/autoq/stations', sub {
		my $data = shift;

		my $current = $client ? $prefs->client($client)->get('stationId') : undef;

		my @items = ( {
			name => ( !$current ? "\x{2713} " : '' ) . cstring($client, 'PLUGIN_MBXHUB_NO_STATION'),
			type => 'link',
			url  => \&selectStationFeed,
			passthrough => [ { id => '', name => '' } ],
			nextWindow  => 'parent',
		} );

		for my $station ( @{ ($data && $data->{stations}) || [] } ) {
			next unless defined $station->{id};

			my $selected = defined $current && $current eq $station->{id};

			push @items, {
				name  => ( $selected ? "\x{2713} " : '' ) . ($station->{name} || $station->{id}),
				line1 => ($station->{name} || $station->{id}),
				line2 => sprintf('%s · %d seeds', $station->{flow} || 'smooth', $station->{seedCount} || 0),
				type  => 'link',
				url   => \&selectStationFeed,
				passthrough => [ { id => $station->{id}, name => $station->{name} } ],
				nextWindow  => 'parent',
			};
		}

		$cb->({ items => \@items });
	});
}

sub selectStationFeed {
	my ($client, $cb, $args, $pass) = @_;

	if ( !$client ) {
		# Browsing without a player context — nothing to attach the pick to.
		return $cb->({ items => [ { name => cstring(undef, 'NO_PLAYER_FOUND'), type => 'text' } ] });
	}

	my $confirmation;

	if ( defined $pass->{id} && $pass->{id} ne '' ) {
		$prefs->client($client)->set('stationId', $pass->{id});
		$log->info("AutoQ station '$pass->{name}' ($pass->{id}) selected for " . $client->name);
		$confirmation = cstring($client, 'PLUGIN_MBXHUB_STATION_SET') . ': ' . ($pass->{name} || $pass->{id});
	}
	else {
		$prefs->client($client)->remove('stationId');
		$log->info('AutoQ station cleared for ' . $client->name);
		$confirmation = cstring($client, 'PLUGIN_MBXHUB_STATION_CLEARED');
	}

	$cb->({ items => [ { name => $confirmation, type => 'text' } ] });
}

# ---------------------------------------------------------------------------
# Don't Stop the Music provider — "AutoQ (MBXHub)".
#
# When LMS wants more music: read the player's picked station, fetch its full
# record (seeds + flow), POST them to /autoq/radio/generate, and hand back
# the picks as /stream/ URLs. Stateless on the Hub side — never touches
# MusicBee's queue or radio run-state. No station picked → empty, gracefully.
# ---------------------------------------------------------------------------

sub dontStopTheMusic {
	my ($client, $cb) = @_;

	return $cb->($client, []) unless $client;

	my $stationId = $prefs->client($client)->get('stationId');

	if ( !defined $stationId || $stationId eq '' ) {
		main::INFOLOG && $log->is_info
			&& $log->info('DSTM asked for tracks but no AutoQ station is selected — returning none');
		return $cb->($client, []);
	}

	Plugins::MBXHub::API->get('/autoq/stations/' . uri_escape_utf8($stationId), sub {
		my $station = shift;

		if ( !$station || ref $station->{seedUrls} ne 'ARRAY' || !@{ $station->{seedUrls} } ) {
			$log->warn("AutoQ station '$stationId' missing or has no seeds — returning no tracks");
			return $cb->($client, []);
		}

		Plugins::MBXHub::API->post('/autoq/radio/generate', {
			seedUrls => $station->{seedUrls},
			flow     => $station->{flow} || 'smooth',
			count    => DSTM_COUNT,
		}, sub {
			my $data = shift;

			my @urls = map  { Plugins::MBXHub::API->streamUrl($_->{url}) }
			           grep { $_->{url} }
			           @{ ($data && $data->{tracks}) || [] };

			main::INFOLOG && $log->is_info
				&& $log->info(sprintf('AutoQ station %s delivered %d tracks', $stationId, scalar @urls));

			$cb->($client, \@urls);
		});
	});
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

# Hub track row ({url, title, artist, album, duration(ms)}) → XMLBrowser item.
# The play URL is always the Hub's /stream/ endpoint; range requests are
# supported there, so seeking works.
sub _trackItem {
	my ($track) = @_;

	my $fileUrl = $track->{url} or return ();

	my $stream = Plugins::MBXHub::API->streamUrl($fileUrl);
	my $title  = ( defined $track->{title} && $track->{title} ne '' )
		? $track->{title}
		: ( $fileUrl =~ m{([^\\/]+)$} ? $1 : $fileUrl );

	my $secs = $track->{duration} ? int($track->{duration} / 1000) : 0;

	return {
		name      => $title,
		line1     => $title,
		line2     => $track->{artist} || '',
		type      => 'audio',
		url       => $stream,
		play      => $stream,
		on_select => 'play',
		playall   => 1,
		image     => Plugins::MBXHub::API->artworkUrl($fileUrl),
		$secs ? ( duration => $secs ) : (),
	};
}

# Empty results render a friendly hint instead of a blank screen.
sub _orEmpty {
	my ($client, $items) = @_;

	return $items if @$items;
	return [ { name => cstring($client, 'PLUGIN_MBXHUB_EMPTY'), type => 'text' } ];
}

1;
