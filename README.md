# Faemiyah.Library

Faemiyah.Library is a collection of C# libraries that house elements my other programs (Arranger, BtDamageResolver, DayAheadPrice) use. To build them, you need a development container with some tools and libraries contained in this repository.

I know this makes it a bit harder to build the projects themselves, but it's much easier for me personally. Probably should add this as git submodule or something.

## Building the SDK

* Get the library (https://github.com/Warmag2/Faemiyah.Library) and clone it
* Go to the Faemiyah.Library root folder
* Run `refresh.sh` in the root directory

This will produce a faemiyahsdk container that the other projects can use to build themselves.

## Individual libraries

Please reference the other README files in the root folder.
