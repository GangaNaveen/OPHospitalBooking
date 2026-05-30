# PWA App Icons Setup Guide

The application is now configured as a Progressive Web App (PWA). To complete the setup, you need to create app icons.

## Required Icons

The `manifest.json` file references the following icons:
- `icon-192x192.png` - Required for Android and web manifest (192x192 pixels)
- `icon-512x512.png` - Required for splash screens (512x512 pixels)

Both icons should be placed in the `wwwroot/images/` directory.

## Creating Icons

You can create these icons using any of the following methods:

### Option 1: Using an Online PWA Icon Generator
1. Go to https://appicon.co/
2. Upload your logo (e.g., logo.png from the images folder)
3. Select PNG format and download the icons
4. Place the 192x192 and 512x512 icons in `wwwroot/images/`

### Option 2: Using PowerShell Script (Windows)
```powershell
# This script uses .NET to resize images
# Install ImageMagick or use a similar tool first
```

### Option 3: Using ImageMagick (Command Line)
```bash
# Install ImageMagick from https://imagemagick.org/
# Then run:
magick convert wwwroot/images/logo.png -resize 192x192 wwwroot/images/icon-192x192.png
magick convert wwwroot/images/logo.png -resize 512x512 wwwroot/images/icon-512x512.png
```

### Option 4: Using Online Image Resizer
1. Go to https://resizeimage.net/
2. Upload logo.png
3. Set size to 192x192 and download as icon-192x192.png
4. Repeat for 512x512 size

## Icon Requirements

- Format: PNG with transparent background (recommended)
- Size: Exactly 192x192 and 512x512 pixels
- Color: Match your app's branding (preferably matching the theme-color #0d6efd)

## After Creating Icons

Once you've created and placed the icons in `wwwroot/images/`:
1. Clear your browser cache
2. Restart the application
3. Open the app in a browser
4. Look for the "Install App" button in the navbar on supported browsers
5. Click to install the PWA

## Supported Browsers

- Chrome/Chromium (Android & Windows): Full PWA support
- Edge: Full PWA support
- Safari (iOS 16.4+): Limited PWA support
- Firefox: Experimental PWA support
