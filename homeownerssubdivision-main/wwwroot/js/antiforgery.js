// Lightweight antiforgery helpers for MVC + AutoValidateAntiforgeryToken.
// - Reads the request token from <meta name="csrf-token" ...>
// - Automatically attaches it to same-origin unsafe requests (fetch/XHR/jQuery)
//
// This avoids having to manually add __RequestVerificationToken to every AJAX call.
(function () {
    "use strict";

    const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS", "TRACE"]);

    function getMetaToken() {
        const el = document.querySelector('meta[name="csrf-token"]');
        const token = el ? el.getAttribute("content") : "";
        return token || "";
    }

    function isSameOrigin(url) {
        if (!url) return true;
        try {
            const resolved = new URL(url, window.location.href);
            return resolved.origin === window.location.origin;
        } catch {
            // If the URL can't be parsed, assume same-origin (relative paths, etc.).
            return true;
        }
    }

    // Expose for debugging/rare edge cases.
    window.__getCsrfToken = function () {
        return getMetaToken();
    };

    // Patch fetch to automatically send the antiforgery token as a header.
    if (typeof window.fetch === "function") {
        const originalFetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            init = init || {};

            const url =
                typeof input === "string"
                    ? input
                    : input && typeof input.url === "string"
                        ? input.url
                        : "";

            const method = (init.method || (input && input.method) || "GET").toString().toUpperCase();
            if (!SAFE_METHODS.has(method) && isSameOrigin(url)) {
                const token = getMetaToken();
                if (token) {
                    const headers = new Headers(init.headers || (input && input.headers) || undefined);
                    if (!headers.has("RequestVerificationToken")) {
                        headers.set("RequestVerificationToken", token);
                    }
                    init.headers = headers;
                }
            }

            return originalFetch(input, init);
        };
    }

    // Patch XMLHttpRequest as a fallback for code that still uses XHR directly.
    if (typeof window.XMLHttpRequest === "function") {
        const originalOpen = XMLHttpRequest.prototype.open;
        const originalSend = XMLHttpRequest.prototype.send;

        XMLHttpRequest.prototype.open = function (method, url) {
            this.__csrfMethod = (method || "GET").toString().toUpperCase();
            this.__csrfUrl = url || "";
            return originalOpen.apply(this, arguments);
        };

        XMLHttpRequest.prototype.send = function () {
            try {
                const method = (this.__csrfMethod || "GET").toString().toUpperCase();
                const url = this.__csrfUrl || "";
                if (!SAFE_METHODS.has(method) && isSameOrigin(url)) {
                    const token = getMetaToken();
                    if (token) {
                        try {
                            this.setRequestHeader("RequestVerificationToken", token);
                        } catch {
                            // Ignore if headers are already sent or request cannot be modified.
                        }
                    }
                }
            } catch {
                // Best-effort only.
            }

            return originalSend.apply(this, arguments);
        };
    }

    // Bind jQuery AJAX if/when jQuery is loaded (some pages load it later via CDN).
    (function bindJQueryWhenAvailable() {
        let attempts = 0;
        const maxAttempts = 80; // ~8s @ 100ms

        function tryBind() {
            const $ = window.jQuery;
            if (!$ || typeof $.ajaxPrefilter !== "function") return false;

            if ($.__csrfPrefilterBound) return true;

            $.ajaxPrefilter(function (options, originalOptions, jqXHR) {
                const type = (options.type || options.method || "GET").toString().toUpperCase();
                if (SAFE_METHODS.has(type)) return;
                if (options.crossDomain) return;

                const token = getMetaToken();
                if (token) {
                    try {
                        jqXHR.setRequestHeader("RequestVerificationToken", token);
                    } catch {
                        // Ignore.
                    }
                }
            });

            $.__csrfPrefilterBound = true;
            return true;
        }

        function tick() {
            attempts += 1;
            if (tryBind()) return;
            if (attempts >= maxAttempts) return;
            setTimeout(tick, 100);
        }

        tick();
    })();
})();

