package Plugins::MBXHub::Settings;

# Web settings page: MBXHub host + port (manual entry, no discovery) and
# optional HTTP Basic Auth credentials applied to every request.

use strict;
use warnings;

use base qw(Slim::Web::Settings);

use Slim::Utils::Prefs;

my $prefs = preferences('plugin.mbxhub');

sub name {
	return Slim::Web::HTTP::CSRF->protectName('PLUGIN_MBXHUB');
}

sub page {
	return Slim::Web::HTTP::CSRF->protectURI('plugins/MBXHub/settings/basic.html');
}

sub prefs {
	return ($prefs, qw(host port username password));
}

1;
