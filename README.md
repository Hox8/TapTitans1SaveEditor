# TapTitans 1 Save Editor
Simple commandline utility for un/packaging Tap Titans 1 save files. Made for a friend, but I've uploaded the source code here in case anyone might find it useful.

## Features
- Extracts all relevant parts of a TapTitans 1 save file into a folder
- Escapes and unescapes the player info JSON payload
- Recalculates and encrypts the checksum for the player info JSON payload
- Reserializes the save file, allowing extending or shrinking the player info JSON payload

This program does not prettify/minify the JSON payload; I'd recommended performing this step this manually.

## Usage
- Drag and TapTitans 1 save file `912af0dff974604f1321254ca8ff38b6.adat` onto the executable to extract its contents into a subfolder.
- After modifying the contents within the extracted subfolder, drag it back onto the exe to repack it into a TapTitans 1 save file.

## Build
Solution is compatible with Windows and Unix machines; preprocessor macros switch out platform-specific code and should run out of the box for all platforms. An x64 Windows release is available in the releases tab.

## Credits
- Zeke for prompting me to make this, as I myself have never played Tap Titans.
- NonsenseCreativity for writing the 'Modding The Tap Titans Save File' article, which got me started.
