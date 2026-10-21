#!/bin/sh
# Builds the faemiyahsdk base image, which packs the Faemiyah.* libraries into NuGet
# packages placed in /build/customnugets. Downstream projects (for example BtDamageResolver's
# resolversdk) build FROM faemiyahsdk and restore these packages.
docker build --tag faemiyahsdk -f ./infra/sdk/Dockerfile .
