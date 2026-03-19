# PhotoDrop

PhotoDrop is a web application that allows event hosts to collect photos from guests through a simple QR code — no guest login required.

Hosts create an event, connect their Google Drive, and receive a shareable QR code. Guests scan the QR code and upload photos directly — files go straight to Google Cloud Storage, then transfer into the host's Google Drive folder.

> This project is currently a Work In Progress.

## Current Features
- Event creation with Google OAuth and automatic Drive folder setup
- QR code generation and download for guest sharing
- Direct-to-cloud photo uploads via presigned URLs (browser → Cloud Storage → Drive)
- Real-time per-file upload progress and status feedback
- Backend file validation (type, size, extension, magic bytes)
- Dark-themed landing page and guest upload UI

## Planned Features
- Rate limiting and abuse prevention
- Database persistence (replace in-memory EventStorage)
- Stored Google refresh tokens for long-lived uploads
- Drag-and-drop photo uploads
- Production deployment

## Tech Stack

### Frontend
- Blazor (.NET 8)
- Razor Components
- HTML / CSS
- JavaScript (for direct-to-cloud uploads via presigned URLs)

### Backend
- C#
- ASP.NET Core
- Google OAuth 2.0
- Google Drive API
- Google Cloud Storage (presigned URL uploads)

### External Services
- Google Cloud Storage — temporary file storage via presigned URLs
- Google Drive API — final destination for guest photos

## Architecture Overview

1. Host creates an event and connects Google Drive via OAuth
2. Server creates a dedicated Drive folder and generates a guest token
3. Host receives a QR code and shareable link for the event
4. Guests scan the QR code — no login required
5. For each photo, the guest's browser requests a presigned upload URL from the server
6. The server validates the file type and generates a temporary Cloud Storage URL
7. The guest's browser uploads directly to Google Cloud Storage (server is not in the upload path)
8. After all uploads complete, the server transfers files from Cloud Storage to the host's Google Drive folder
9. Files are deleted from Cloud Storage after transfer

# Project Status
PhotoDrop is under active development. The core upload pipeline is fully functional — guests can scan a QR code and upload photos that land in the host's Google Drive. Current focus is on UI polish, abuse prevention, and preparing for database persistence and production deployment.
