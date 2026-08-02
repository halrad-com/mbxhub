package Plugins::MBXHub::API;

# Thin async REST client for MBXHub (https://mbxhub.com/llms.txt).
#
# Every response is the standard Hub envelope {success, data} on 200 or
# {success:false, error:{code,message}} on failure. Callbacks always fire
# exactly once and receive either the unwrapped `data` hashref or undef —
# callers render an empty menu / return no tracks instead of hanging.
#
# All calls are LAN HTTP with a short timeout. Optional HTTP Basic Auth is
# applied as a header on API calls, and embedded as user:pass@ in the URLs
# handed to players (streams, artwork) since players fetch those directly.

use strict;
use warnings;

use JSON::XS::VersionOneAndTwo;
use MIME::Base64 qw(encode_base64);
use URI::Escape qw(uri_escape_utf8);

use Slim::Networking::SimpleAsyncHTTP;
use Slim::Utils::Log;
use Slim::Utils::Prefs;

use constant TIMEOUT => 10;   # seconds — LAN or bust

my $log   = logger('plugin.mbxhub');
my $prefs = preferences('plugin.mbxhub');

sub _hostPort {
	my $host = $prefs->get('host') || '127.0.0.1';
	my $port = $prefs->get('port') || 8080;
	$host =~ s/^\s+|\s+$//g;
	return ($host, $port);
}

# Base URL for API calls (auth goes in a header).
sub baseUrl {
	my ($host, $port) = _hostPort();
	return "http://$host:$port";
}

# Base URL for player-fetched resources (streams, artwork). Players issue
# these requests themselves, so credentials ride in the URL when configured.
sub mediaBaseUrl {
	my ($host, $port) = _hostPort();
	my $user = $prefs->get('username');
	my $pass = $prefs->get('password');

	if (defined $user && $user ne '') {
		return sprintf('http://%s:%s@%s:%s',
			uri_escape_utf8($user), uri_escape_utf8($pass // ''), $host, $port);
	}

	return "http://$host:$port";
}

sub _authHeaders {
	my $user = $prefs->get('username');
	my $pass = $prefs->get('password');

	if (defined $user && $user ne '') {
		return ('Authorization' => 'Basic ' . encode_base64("$user:" . ($pass // ''), ''));
	}

	return ();
}

sub _unwrap {
	my ($http, $cb, $what) = @_;

	my $data = eval { from_json($http->content) };

	if ($@ || ref $data ne 'HASH') {
		$log->warn("MBXHub $what: response was not valid JSON: " . ($@ || 'not a hash'));
		return $cb->(undef);
	}

	if (!$data->{success}) {
		my $err = ref $data->{error} eq 'HASH' ? ($data->{error}{code} || 'ERROR') : 'ERROR';
		$log->warn("MBXHub $what: hub returned error $err");
		return $cb->(undef);
	}

	$cb->($data->{data});
}

# get('/library/albums?limit=100', sub { my $data = shift; ... });
sub get {
	my ($class, $path, $cb) = @_;

	my $url = baseUrl() . $path;
	main::DEBUGLOG && $log->is_debug && $log->debug("GET $url");

	Slim::Networking::SimpleAsyncHTTP->new(
		sub { _unwrap(shift, $cb, "GET $path") },
		sub {
			my ($http, $error) = @_;
			$log->warn("MBXHub GET $path failed: " . ($error || 'unknown error'));
			$cb->(undef);
		},
		{ timeout => TIMEOUT, cache => 0 },
	)->get($url, 'Accept' => 'application/json', _authHeaders());
}

# post('/autoq/radio/generate', { seedUrls => [...] }, sub { ... });
sub post {
	my ($class, $path, $body, $cb) = @_;

	my $url = baseUrl() . $path;
	my $json = eval { to_json($body) };

	if ($@ || !defined $json) {
		$log->warn("MBXHub POST $path: could not serialize request body: $@");
		return $cb->(undef);
	}

	main::DEBUGLOG && $log->is_debug && $log->debug("POST $url $json");

	Slim::Networking::SimpleAsyncHTTP->new(
		sub { _unwrap(shift, $cb, "POST $path") },
		sub {
			my ($http, $error) = @_;
			$log->warn("MBXHub POST $path failed: " . ($error || 'unknown error'));
			$cb->(undef);
		},
		{ timeout => TIMEOUT, cache => 0 },
	)->post($url,
		'Accept'       => 'application/json',
		'Content-Type' => 'application/json',
		_authHeaders(),
		$json);
}

# GET /stream/{URL-encoded absolute file path} — range requests supported,
# so seeking works. This is the only play URL the plugin ever emits.
sub streamUrl {
	my ($class, $fileUrl) = @_;
	return mediaBaseUrl() . '/stream/' . uri_escape_utf8($fileUrl);
}

# GET /library/file/{url}/artwork — binary cover art for a track.
sub artworkUrl {
	my ($class, $fileUrl) = @_;
	return mediaBaseUrl() . '/library/file/' . uri_escape_utf8($fileUrl) . '/artwork';
}

1;
