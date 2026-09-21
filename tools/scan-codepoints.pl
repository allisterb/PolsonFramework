#!/usr/bin/perl
# scan-codepoints.pl <dir> [...]
#
# Read-only codepoint-level scan of third-party / reference material, per the
# project guardrails in CLAUDE.md. Reports the codepoint classes that are used
# to smuggle hidden text, plus a census of all non-ASCII so a human can judge
# whether it is benign (typography, foreign-language comments, box drawing).
#
# Opens every file with '<:raw' and never writes: this script must not be able
# to re-encode the material it inspects.
use strict; use warnings;
use Encode qw(decode);
use File::Find;

binmode(STDOUT, ':encoding(UTF-8)');
my @roots = @ARGV or die "usage: $0 <dir|file> [...]\n";

# class => [label, matcher]
my @DANGER = (
    ['bidi override / isolate', sub { my $c=shift; ($c>=0x202A && $c<=0x202E) || ($c>=0x2066 && $c<=0x2069) }],
    ['zero-width / joiner',     sub { my $c=shift; ($c>=0x200B && $c<=0x200F) || $c==0x2060 || $c==0xFEFF }],
    ['soft hyphen',             sub { my $c=shift; $c==0x00AD }],
    ['Unicode Tag block',       sub { my $c=shift; $c>=0xE0000 && $c<=0xE007F }],
    ['private use area',        sub { my $c=shift; ($c>=0xE000 && $c<=0xF8FF) || ($c>=0xF0000 && $c<=0x10FFFD) }],
    ['C0/C1 control',           sub { my $c=shift; ($c<0x09) || ($c>=0x0E && $c<0x20) || ($c>=0x7F && $c<0xA0) }],
);

my @PHRASE = (
    qr/ignore\s+(all\s+)?previous\s+instructions/i, qr/disregard\s+(the\s+)?above/i,
    qr/you\s+are\s+now\s+/i, qr/system\s*prompt/i, qr/<\|[a-z_]+\|>/i,
    qr/\[INST\]|\[\/INST\]/i, qr/###\s*(instruction|system)/i,
    qr/\bAI\s+(assistant|agent)\b.{0,40}\b(must|should|shall)\b/i,
);

my (%census, %hits, %phrases, $nfiles, $nbytes, $nbad_utf8);

sub scan_file {
    my $p = shift;
    return if -d $p || -l $p;
    open(my $h, '<:raw', $p) or do { warn "skip $p: $!\n"; return };
    my $raw = do { local $/; <$h> }; close $h;
    return if $raw =~ /\x00/;                       # binary: nothing to read
    $nfiles++; $nbytes += length $raw;

    my $txt = eval { decode('UTF-8', $raw, Encode::FB_CROAK) };
    if (!defined $txt) { $nbad_utf8++; $txt = decode('UTF-8', $raw) }

    my $bom = ($txt =~ /^\x{FEFF}/) ? 1 : 0;
    my $i = 0;
    for my $ch (split //, $txt) {
        my $c = ord $ch; $i++;
        # Printable ASCII, not "below 0x80". The earlier form skipped every C0 control, which made
        # the C0/C1 class below unreachable: a file carrying U+0001 scanned clean. Ported to
        # src/Polson.Runtime/TextScan.cs, where the same defect was caught by test.
        next if $c >= 0x20 && $c < 0x7F;
        next if $c == 0x0A || $c == 0x0D || $c == 0x09;
        $census{$c}++;
        next if $bom && $i == 1 && $c == 0xFEFF;    # leading BOM is benign
        for my $d (@DANGER) {
            next unless $d->[1]->($c);
            push @{ $hits{$d->[0]} }, sprintf('%s U+%04X', $p, $c);
            last;
        }
    }
    for my $re (@PHRASE) {
        push @{ $phrases{$p} }, $1 while $txt =~ /($re)/g;
    }
}

find({ wanted => sub { scan_file($File::Find::name) }, no_chdir => 1 }, @roots);

printf "scanned %d text files, %d bytes\n", $nfiles, $nbytes;
printf "!! %d file(s) were not well-formed UTF-8\n", $nbad_utf8 if $nbad_utf8;

print "\n-- non-ASCII census (codepoint, count, char) --\n";
for my $c (sort { $census{$b} <=> $census{$a} || $a <=> $b } keys %census) {
    printf "  U+%04X  %7d  %s\n", $c, $census{$c},
        ($c >= 0x20 && $c != 0x7F ? chr($c) : '.');
}

print "\n-- suspicious codepoint classes --\n";
if (!%hits) { print "  none\n" }
else { for my $k (sort keys %hits) {
    my %u; $u{$_}++ for @{$hits{$k}};
    printf "  %-24s %d occurrence(s)\n", $k, scalar @{$hits{$k}};
    printf "      %s (x%d)\n", $_, $u{$_} for (sort keys %u)[0..($#{[keys %u]} > 4 ? 4 : $#{[keys %u]})];
} }

print "\n-- injection phrasing --\n";
if (!%phrases) { print "  none\n" }
else { for my $p (sort keys %phrases) { printf "  %s: %s\n", $p, join('; ', @{$phrases{$p}}) } }
