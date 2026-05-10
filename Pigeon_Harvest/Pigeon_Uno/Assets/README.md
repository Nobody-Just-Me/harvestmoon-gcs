# Assets Directory

This directory contains all application assets:

## Structure:
- `fonts/` - Application fonts (Raleway, Commando, etc.)
- `icons/` - UI icons and symbols
- `images/` - Background images and graphics
- `logo/` - Company and product logos
- `models/` - AI/ML models (YOLO, etc.)

## Usage:
Assets are referenced using `ms-appx:///Assets/` URI scheme in XAML and code.

Example:
```xml
<Image Source="ms-appx:///Assets/logo/pigeon-logo.png" />
```
