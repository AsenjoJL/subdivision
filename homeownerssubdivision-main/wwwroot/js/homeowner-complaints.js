(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function toArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function setAlert(root, element, message) {
        if (!element) {
            return;
        }

        const textNode = element.querySelector('span');
        if (textNode) {
            textNode.textContent = message || '';
        }

        element.classList.toggle('homeowner-complaint-hidden', !message);
        if (message) {
            window.clearTimeout(element.__timerId);
            element.__timerId = window.setTimeout(function () {
                element.classList.add('homeowner-complaint-hidden');
            }, 3200);
        }
    }

    function clearValidation(root) {
        root.querySelectorAll('.homeowner-complaints-field').forEach((field) => field.classList.remove('is-invalid'));
        root.querySelectorAll('[data-validation-for]').forEach((node) => {
            node.textContent = '';
        });
    }

    function applyValidation(root, validationErrors) {
        if (!validationErrors) {
            return;
        }

        Object.entries(validationErrors).forEach(([key, messages]) => {
            const fieldError = root.querySelector(`[data-validation-for="${key}"]`);
            if (fieldError) {
                fieldError.textContent = toArray(messages).join(' ');
            }

            root.querySelector(`[data-field="${key}"]`)?.closest('.homeowner-complaints-field')?.classList.add('is-invalid');
        });
    }

    async function refreshList(root) {
        const listHost = root.querySelector('[data-complaint-list]');
        const loadUrl = listHost?.getAttribute('data-load-url');
        if (!listHost || !loadUrl) {
            return;
        }

        listHost.classList.add('is-loading');
        try {
            const response = await fetch(loadUrl, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) {
                throw new Error('Failed to refresh complaints.');
            }

            listHost.innerHTML = await response.text();

            const cacheUrl = root.getAttribute('data-cache-url');
            if (cacheUrl && window.homeownerDashboardShell?.updateCache) {
                window.homeownerDashboardShell.updateCache(cacheUrl, root.outerHTML);
            }
        } finally {
            listHost.classList.remove('is-loading');
        }
    }

    const initializeHomeownerComplaints = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="complaints"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="complaints"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('submit', async function (event) {
            const form = event.target.closest('[data-complaint-form]');
            if (!form) {
                return;
            }

            event.preventDefault();

            const successAlert = root.querySelector('[data-complaint-success]');
            const errorAlert = root.querySelector('[data-complaint-error]');
            const submitButton = form.querySelector('[data-complaint-submit]');

            clearValidation(root);
            setAlert(root, successAlert, '');
            setAlert(root, errorAlert, '');
            submitButton.disabled = true;

            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: new FormData(form)
                });

                const result = await response.json();
                if (!response.ok || !result.success) {
                    applyValidation(root, result.validationErrors);
                    setAlert(root, errorAlert, result.message || 'Unable to submit complaint.');
                    return;
                }

                form.reset();
                await refreshList(root);
                setAlert(root, successAlert, result.message || 'Complaint submitted successfully.');
            } catch (error) {
                setAlert(root, errorAlert, 'Unable to submit complaint right now. Please try again.');
            } finally {
                submitButton.disabled = false;
            }
        });
    };

    window.initializeHomeownerComplaints = initializeHomeownerComplaints;
    getDashboardModuleInitializers().complaints = initializeHomeownerComplaints;
})();
