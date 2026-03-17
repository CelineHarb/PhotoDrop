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