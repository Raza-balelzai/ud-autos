# UD Shop installer files

Copy the files produced by the UD Shop release script into this folder before publishing UD Autos:

- `UDShop.appinstaller`
- `UDShop_<version>_x64.msix`
- `release-notes.json`
- `UDShop.cer`

Do not put a `.pfx` file or a certificate password here.

These files are public. The dashboard download button is visible only to signed-in users, but Windows App Installer cannot log in to UD Autos when it checks for updates. The deployment steps are in the UD Shop repository at `installer/README.md`.
