const activeZones = new Map();

export function initialize(inputId) {
    dispose(inputId);

    const input = document.getElementById(inputId);
    const zone = input?.closest('[data-import-dropzone]');
    if (!input || !zone) return;

    const preventDefault = event => {
        event.preventDefault();
        event.stopPropagation();
    };
    const onDragOver = event => {
        preventDefault(event);
        if (!input.disabled) zone.classList.add('is-dragging');
    };
    const onDragLeave = event => {
        preventDefault(event);
        if (!zone.contains(event.relatedTarget)) zone.classList.remove('is-dragging');
    };
    const onDrop = event => {
        preventDefault(event);
        zone.classList.remove('is-dragging');
        if (input.disabled) return;

        const files = event.dataTransfer?.files;
        if (!files?.length) return;

        const transfer = new DataTransfer();
        transfer.items.add(files[0]);
        input.files = transfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };

    for (const eventName of ['dragenter', 'dragover', 'dragleave', 'drop'])
        zone.addEventListener(eventName, preventDefault);
    zone.addEventListener('dragover', onDragOver);
    zone.addEventListener('dragleave', onDragLeave);
    zone.addEventListener('drop', onDrop);

    activeZones.set(inputId, () => {
        for (const eventName of ['dragenter', 'dragover', 'dragleave', 'drop'])
            zone.removeEventListener(eventName, preventDefault);
        zone.removeEventListener('dragover', onDragOver);
        zone.removeEventListener('dragleave', onDragLeave);
        zone.removeEventListener('drop', onDrop);
        zone.classList.remove('is-dragging');
    });
}

export function dispose(inputId) {
    activeZones.get(inputId)?.();
    activeZones.delete(inputId);
}
