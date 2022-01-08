#!/bin/bash -Eeu

PLATFORM=`uname -s`

function GetRidArchitecture()
{
    ARCHITECTURE=`uname -m`
    if [[ $ARCHITECTURE == 'x86_64' ]]; then
        echo "x64"
    elif [[ $ARCHITECTURE == 'aarch64' ]]; then
        echo "arm64"
    # ARM32 includes the version (IE: armv7l) so only match the beginning of the string
    elif [[ $ARCHITECTURE == arm* ]]; then
        echo "arm"
    else
        echo "Unrecognized processor architecture '$ARCHITECTURE'. Not sure what to use for RID." 1>&2
        echo "You can force a specific RID by setting FORCE_RID." 1>&2
        return 1
    fi
}

if [[ -n ${FORCE_RID:-} ]]; then
    echo "$FORCE_RID"
elif [[ $PLATFORM == 'Linux' ]]; then
    RID_ARCHITECTURE=`GetRidArchitecture`
    echo "linux-$RID_ARCHITECTURE"
elif [[ $PLATFORM == 'Darwin' ]]; then
    RID_ARCHITECTURE=`GetRidArchitecture`
    echo "osx-$RID_ARCHITECTURE"
elif [[ $PLATFORM == MINGW* ]]; then
    echo "MINGW (and Git Bash) are not supported, use the Windows-specific build scripts instead." >&2
    exit 1
elif [[ $PLATFORM == CYGWIN* ]]; then
    echo "CYGWIN is not supported, use the Windows-specific build scripts instead." >&2
    exit 1
else
    echo "Unrecognized platform '$PLATFORM'. Not sure what to use for RID." >&2
    echo "You can force a specific RID by setting FORCE_RID." >&2
    exit 1
fi
