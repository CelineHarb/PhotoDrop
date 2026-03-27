// Uploads file bytes directly to Google Cloud Storage using a presigned URL.
window.uploadFileToCloud = async function (uploadUrl, fileBytes, contentType) {
    const response = await fetch(uploadUrl, {
        method: 'PUT',
        headers: {
            'Content-Type': contentType
        },
        body: new Uint8Array(fileBytes)
    });
    return response.ok;
};

// Creates a temporary preview URL for a file selected via InputFile.
// We store files in a map so we can access them later for upload.
window._photoDropFiles = {};

window.createPreviews = function (inputElement) {
    const files = inputElement.files;
    const previews = [];
    window._photoDropFiles = {};

    for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const url = URL.createObjectURL(file);
        window._photoDropFiles[i] = file;
        previews.push({
            index: i,
            name: file.name,
            size: file.size,
            type: file.type,
            previewUrl: url
        });
    }
    return previews;
};

// Uploads a file by index directly to Cloud Storage using a presigned URL.
window.uploadFileByIndex = async function (index, uploadUrl) {
    const file = window._photoDropFiles[index];
    if (!file) return false;

    try {
        const response = await fetch(uploadUrl, {
            method: 'PUT',
            headers: {
                'Content-Type': file.type
            },
            body: file
        });
        return response.ok;
    } catch {
        return false;
    }
};

// Cleans up preview URLs to free memory.
window.revokePreviews = function (urls) {
    for (const url of urls) {
        URL.revokeObjectURL(url);
    }
};