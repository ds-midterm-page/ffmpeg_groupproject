let objectUrl;
export function release() {
    if (objectUrl) URL.revokeObjectURL(objectUrl);
    objectUrl = undefined;
}
export function preview(input) {
    release();
    objectUrl = URL.createObjectURL(input.files[0]);
    return objectUrl;
}
export function currentTime(video) { return video.currentTime; }
