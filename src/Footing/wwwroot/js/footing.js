window.Footing = {
    initializePopovers: function () {
        var isTouchDevice = ("ontouchstart" in window) || window.DocumentTouch && document instanceof DocumentTouch;
        document.querySelectorAll('[data-bs-toggle="popover"]').forEach(function (el) {
            new bootstrap.Popover(el, {
                trigger: isTouchDevice ? "click" : "hover"
            });
        });
    },

    toggleTheme: function () {
        var html = document.documentElement;
        var current = html.getAttribute('data-theme');
        if (!current) {
            current = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        }
        var next = current === 'dark' ? 'light' : 'dark';
        html.setAttribute('data-theme', next);
        localStorage.setItem('ft-theme', next);
    },

    saveAsFile: function (filename, mimeType, bytesBase64) {
        var data = window.atob(bytesBase64);
        var bytes = new Uint8Array(data.length);
        for (var i = 0; i < data.length; i++) {
            bytes[i] = data.charCodeAt(i);
        }
        var blob = new Blob([bytes.buffer], { type: mimeType });
        var link = document.createElement('a');
        link.download = filename;
        link.href = URL.createObjectURL(blob);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
    }
};
