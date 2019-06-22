# HSNXT.Greed

Greed is a toolkit for managing a greedy amount (like a billion) of add-ons for
[Garry's Mod](http://en.wikipedia.org/wiki/Garry%27s_Mod). It is based on @whisperity's fantastic
[SharpGMad](https://github.com/whisperity/SharpGMad) utility, but adapted to .NET Standard/.NET Core. Greed includes the
complete command-line API from SharpGMad, but does not feature the WinForms-based GUI functionality.

At its core, Greed comes in the form of a library, HSNXT.Greed, which targets the .NET Standard 2.0 framework. This
library can be used to interact with `.gma` format add-on files for Garry's Mod without extracting them (as you would do
with Garry Newman's `gmad` utility). The add-ons can also be modified in real-time, by adding, modifying or removing
files.

The HSNXT.Greed.CommandLine application implements SharpGMad's command-line functionality. You usually will not need to
touch this, as SharpGMad is far more complete. You may however want to use it if you're on a GNU/Linux machine, as it's
completely platform-agnostic.

The HSNXT.Greed.AddonGrep application is the only reason this repository was created to begin with, and probably the
only thing that might catch your interest. It's a command-line utility that can be used to find files within add-ons in
a (presumably) massive `addons` folder without having to extract them, providing excellent performance (on my machine,
it can search through 500+ add-ons in a few seconds). The output of the application is also as consistent as you could
expect, so you can feed it into whatever command-line utility you wish to chew through the data.

There are other tools, but I haven't finished them yet. Maybe I'll write a blurb here once I do.

### Disclaimer

This program is provided AS IS and if you MAKE ANY MISTAKES I am GOING to LAUGH AT YOU. WHY ARE WE SHOUTING.

This program is not supported or endorsed by Whisperity, the SharpGMad contributors, or the Pope.