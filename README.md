# PhotoDrop

PhotoDrop is a web application that allows event hosts to collect photos from guests through a simple QR code — no guest login required.

Hosts create an event, connect their Google Drive, and receive a shareable QR code. Guests scan the QR code and upload photos directly into the host’s Google Drive folder.

> This project is currently a Work In Progress.

## Current Features
- Landing page UI
- Event creation flow
- Google OAuth integration (in progress)
- Google Drive folder creation (in progress)
- QR code generation (planned)
- Photo upload pipeline (planned)

## Planned Features
- Secure photo upload pipeline (guest → server → Google Drive)
- Rate limiting & abuse prevention
- File type and size validation (JPG / JPEG / PNG)
- Downloadable QR code
- Production deployment

## Tech Stack

### Frontend
- Blazor (.NET 8)
- Razor Components
- HTML / CSS

### Backend
- ASP.NET Core
- Google OAuth 2.0
- Google Drive API

## Architecture Overview

1. Host creates event
2. Host connects Google Drive via OAuth
3. Server securely stores OAuth tokens
4. Server creates a dedicated Drive folder for the event
5. Guests upload photos via a public event link
6. Server uploads photos into the host’s Drive folder

# Project Status
PhotoDrop is currently under active development as part of building a production-ready event photo collection platform.
Core OAuth integration is in progress. Additional features such as secure upload handling, QR generation, and production deployment are planned next.