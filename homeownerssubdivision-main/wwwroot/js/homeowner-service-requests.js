(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function toArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function setAlert(alertElement, message) {
        if (!alertElement) {
            return;
        }

        const textNode = alertElement.querySelector('span');
        if (textNode) {
            textNode.textContent = message || '';
        }

        alertElement.classList.toggle('service-request-hidden', !message);
    }

    function clearValidationState(root) {
        root.querySelectorAll('.service-request-field').forEach((field) => field.classList.remove('is-invalid'));
        root.querySelectorAll('.service-request-priority-group').forEach((group) => group.classList.remove('is-invalid'));
        root.querySelectorAll('[data-validation-for]').forEach((node) => {
            node.textContent = '';
        });
    }

    function applyValidationErrors(root, validationErrors) {
        if (!validationErrors) {
            return;
        }

        Object.entries(validationErrors).forEach(([key, messages]) => {
            const fieldError = root.querySelector(`[data-validation-for="${key}"]`);
            if (fieldError) {
                fieldError.textContent = toArray(messages).join(' ');
            }

            const field = root.querySelector(`[data-field="${key}"]`);
            if (field) {
                const fieldContainer = field.closest('.service-request-field');
                fieldContainer?.classList.add('is-invalid');
            }

            if (key === 'NewRequest.Priority') {
                root.querySelector('.service-request-priority-group')?.classList.add('is-invalid');
            }
        });
    }

    function syncModuleCache(root) {
        const cacheUrl = root.getAttribute('data-cache-url');
        if (!cacheUrl || !window.homeownerDashboardShell?.updateCache) {
            return;
        }

        window.homeownerDashboardShell.updateCache(cacheUrl, root.outerHTML);
    }

    async function refreshList(root) {
        const listHost = root.querySelector('[data-service-request-list]');
        if (!listHost) {
            return;
        }

        const loadUrl = listHost.getAttribute('data-load-url');
        if (!loadUrl) {
            return;
        }

        listHost.classList.add('is-loading');
        try {
            const response = await fetch(loadUrl, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error('Failed to refresh service requests.');
            }

            listHost.innerHTML = await response.text();
            syncModuleCache(root);
        } finally {
            listHost.classList.remove('is-loading');
        }
    }

    async function handleSubmit(root, event) {
        const form = event.target.closest('[data-service-request-form]');
        if (!form) {
            return;
        }

        event.preventDefault();
        clearValidationState(root);

        const submitButton = form.querySelector('[data-service-request-submit]');
        if (submitButton) {
            submitButton.disabled = true;
        }

        setAlert(root.querySelector('[data-service-request-success]'), '');
        setAlert(root.querySelector('[data-service-request-error]'), '');

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
                applyValidationErrors(root, result.validationErrors);
                setAlert(root.querySelector('[data-service-request-error]'), result.message || 'Unable to submit request right now.');
                return;
            }

            form.reset();
            setAlert(root.querySelector('[data-service-request-success]'), result.message || 'Request submitted successfully.');
            await refreshList(root);
            syncModuleCache(root);
        } catch (error) {
            setAlert(root.querySelector('[data-service-request-error]'), 'Unable to submit request right now. Please try again.');
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    async function handleCancel(root, button) {
        if (!window.confirm('Cancel this pending request?')) {
            return;
        }

        button.disabled = true;

        try {
            const formData = new FormData();
            formData.append('requestId', button.getAttribute('data-request-id') || '');

            const token = root.querySelector('input[name="__RequestVerificationToken"]');
            if (token) {
                formData.append('__RequestVerificationToken', token.value);
            }

            const response = await fetch(button.getAttribute('data-cancel-url') || '', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root.querySelector('[data-service-request-error]'), result.message || 'Unable to cancel request.');
                return;
            }

            setAlert(root.querySelector('[data-service-request-success]'), 'Request cancelled successfully.');
            setAlert(root.querySelector('[data-service-request-error]'), '');
            await refreshList(root);
            syncModuleCache(root);
        } catch (error) {
            setAlert(root.querySelector('[data-service-request-error]'), 'Unable to cancel request right now. Please try again.');
        } finally {
            button.disabled = false;
        }
    }

    const initializeHomeownerServiceRequests = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="service-requests"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="service-requests"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('submit', function (event) {
            if (event.target.closest('[data-service-request-form]')) {
                handleSubmit(root, event);
            }
        });

        root.addEventListener('click', function (event) {
            const cancelButton = event.target.closest('[data-cancel-request]');
            if (cancelButton) {
                handleCancel(root, cancelButton);
            }
        });
    };

    window.initializeHomeownerServiceRequests = initializeHomeownerServiceRequests;
    getDashboardModuleInitializers().serviceRequests = initializeHomeownerServiceRequests;
})();
