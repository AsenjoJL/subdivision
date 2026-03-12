(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function setAlert(root, type, message) {
        const success = root.querySelector('[data-visitor-pass-success]');
        const error = root.querySelector('[data-visitor-pass-error]');
        const target = type === 'success' ? success : error;
        const other = type === 'success' ? error : success;

        [success, error].forEach((node) => {
            if (!node) {
                return;
            }
            window.clearTimeout(node.__timerId);
        });

        if (other) {
            other.classList.add('visitor-pass-hidden');
        }

        if (!target) {
            return;
        }

        const text = target.querySelector('span');
        if (text) {
            text.textContent = message || '';
        }

        if (!message) {
            target.classList.add('visitor-pass-hidden');
            return;
        }

        target.classList.remove('visitor-pass-hidden');
        target.__timerId = window.setTimeout(function () {
            target.classList.add('visitor-pass-hidden');
        }, 3200);
    }

    function clearValidation(root) {
        root.querySelectorAll('[data-validation-for]').forEach((node) => {
            node.textContent = '';
        });
        root.querySelectorAll('.visitor-pass-field').forEach((field) => {
            field.classList.remove('is-invalid');
        });
    }

    function applyValidation(root, errors) {
        Object.entries(errors || {}).forEach(([key, messages]) => {
            const fieldName = key.split('.').pop();
            const fieldError = root.querySelector(`[data-validation-for="${fieldName}"]`);
            if (fieldError) {
                fieldError.textContent = Array.isArray(messages) ? messages.join(' ') : (messages || '');
            }

            const field = root.querySelector(`[data-field="${fieldName}"]`);
            field?.closest('.visitor-pass-field')?.classList.add('is-invalid');
        });
    }

    function syncModuleCache(root) {
        const cacheUrl = root.getAttribute('data-cache-url');
        if (cacheUrl && window.homeownerDashboardShell?.updateCache) {
            window.homeownerDashboardShell.updateCache(cacheUrl, root.outerHTML);
        }
    }

    async function refreshPasses(root) {
        const list = root.querySelector('[data-visitor-pass-list]');
        const loadUrl = list?.getAttribute('data-load-url');
        if (!list || !loadUrl) {
            return;
        }

        list.classList.add('is-loading');
        try {
            const response = await fetch(loadUrl, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) {
                throw new Error('Unable to refresh visitor passes.');
            }

            list.innerHTML = await response.text();
            syncModuleCache(root);
        } finally {
            list.classList.remove('is-loading');
        }
    }

    async function handleSubmit(root, event) {
        const form = event.target.closest('[data-visitor-pass-form]');
        if (!form) {
            return;
        }

        event.preventDefault();
        clearValidation(root);
        setAlert(root, 'success', '');
        setAlert(root, 'error', '');

        const submit = form.querySelector('[data-visitor-pass-submit]');
        if (submit) {
            submit.disabled = true;
            submit.classList.add('is-loading');
        }

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: new FormData(form)
            });

            const contentType = response.headers.get('content-type') || '';
            if (!contentType.includes('application/json')) {
                throw new Error('The visitor pass request returned an unexpected response.');
            }

            const result = await response.json();
            if (!response.ok || !result.success) {
                applyValidation(root, result.validationErrors);
                setAlert(root, 'error', result.message || 'Unable to submit this request.');
                return;
            }

            form.reset();
            const visitDate = form.querySelector('input[name="VisitDate"]');
            if (visitDate) {
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);
                visitDate.value = tomorrow.toISOString().slice(0, 10);
            }

            setAlert(root, 'success', result.message || 'Visitor pass requested successfully!');
            await refreshPasses(root);
            syncModuleCache(root);
        } catch (error) {
            setAlert(root, 'error', 'Unable to submit this request right now. Please try again.');
        } finally {
            if (submit) {
                submit.disabled = false;
                submit.classList.remove('is-loading');
            }
        }
    }

    const initializeHomeownerVisitorPasses = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="visitor-pass"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="visitor-pass"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('submit', function (event) {
            if (event.target.closest('[data-visitor-pass-form]')) {
                handleSubmit(root, event);
            }
        });
    };

    window.initializeHomeownerVisitorPasses = initializeHomeownerVisitorPasses;
    getDashboardModuleInitializers().visitorPasses = initializeHomeownerVisitorPasses;
})();
