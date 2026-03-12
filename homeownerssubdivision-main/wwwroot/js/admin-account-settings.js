(function () {
    function setLoading(button, loadingText) {
        if (!button) {
            return function () {};
        }

        const originalMarkup = button.innerHTML;
        button.disabled = true;
        button.classList.add('is-loading');
        button.innerHTML = '<i class="fas fa-spinner"></i><span>' + loadingText + '</span>';

        return function restore() {
            button.disabled = false;
            button.classList.remove('is-loading');
            button.innerHTML = originalMarkup;
        };
    }

    function bindAlertDismiss(root, selector) {
        root.querySelectorAll(selector).forEach(function (button) {
            button.addEventListener('click', function () {
                button.closest('.admin-settings-alert')?.classList.add('admin-settings-hidden');
            });
        });
    }

    function clearFieldErrors(root, prefix) {
        root.querySelectorAll('[data-' + prefix + '-error]').forEach(function (element) {
            element.textContent = '';
            element.classList.add('admin-settings-hidden');
        });

        root.querySelectorAll('[data-' + prefix + '-input]').forEach(function (input) {
            input.classList.remove('owner-invalid');
            input.removeAttribute('aria-invalid');
        });
    }

    function applyFieldErrors(root, prefix, payload, fallbackError) {
        clearFieldErrors(root, prefix);

        if (!payload) {
            return;
        }

        const validationErrors = payload.validationErrors || {};
        const directField = payload.field && payload.message ? { [payload.field]: [payload.message] } : null;
        const errorMap = directField || validationErrors;

        let visibleError = false;
        Object.keys(errorMap).forEach(function (fieldName) {
            const message = errorMap[fieldName]?.[0];
            if (!message) {
                return;
            }

            const input = root.querySelector('[data-' + prefix + '-input="' + fieldName + '"]');
            const error = root.querySelector('[data-' + prefix + '-error="' + fieldName + '"]');

            if (input) {
                input.classList.add('owner-invalid');
                input.setAttribute('aria-invalid', 'true');
            }

            if (error) {
                error.textContent = message;
                error.classList.remove('admin-settings-hidden');
                visibleError = true;
            }
        });

        if (!visibleError && fallbackError) {
            fallbackError(payload.message || 'Please correct the highlighted fields.');
        }
    }

    function showAlert(root, kind, message, ids) {
        const successAlert = root.querySelector('#' + ids.success);
        const errorAlert = root.querySelector('#' + ids.error);
        const successMessage = root.querySelector('#' + ids.successMessage);
        const errorMessage = root.querySelector('#' + ids.errorMessage);

        successAlert?.classList.add('admin-settings-hidden');
        errorAlert?.classList.add('admin-settings-hidden');

        if (kind === 'success') {
            if (successMessage) {
                successMessage.textContent = message;
            }
            successAlert?.classList.remove('admin-settings-hidden');
            return;
        }

        if (errorMessage) {
            errorMessage.textContent = message;
        }
        errorAlert?.classList.remove('admin-settings-hidden');
    }

    async function submitForm(root, form, prefix, ids, button, successHandler) {
        const restoreButton = setLoading(button, 'Saving...');

        try {
            const body = new URLSearchParams(new FormData(form));
            const response = await fetch(form.action, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin',
                body: body.toString()
            });

            const payload = await response.json();
            if (!response.ok || !payload || payload.success === false) {
                applyFieldErrors(root, prefix, payload, function (message) {
                    showAlert(root, 'error', message, ids);
                });
                if (!payload?.validationErrors && !(payload?.field && payload?.message)) {
                    showAlert(root, 'error', payload?.message || 'Unable to save changes.', ids);
                }
                return;
            }

            showAlert(root, 'success', payload.message || 'Saved successfully!', ids);
            successHandler?.(payload);
        } catch (error) {
            console.error(error);
            showAlert(root, 'error', 'Unable to save changes right now.', ids);
        } finally {
            restoreButton();
        }
    }

    window.initAdminProfileSettingsModule = function initAdminProfileSettingsModule(root) {
        if (!root || root.dataset.adminProfileInitialized === 'true') {
            return;
        }

        root.dataset.adminProfileInitialized = 'true';
        const form = root.querySelector('#adminProfileForm');
        const submitButton = root.querySelector('[data-admin-profile-submit="true"]');
        const alertIds = {
            success: 'adminProfileSuccessAlert',
            error: 'adminProfileErrorAlert',
            successMessage: 'adminProfileSuccessMessage',
            errorMessage: 'adminProfileErrorMessage'
        };

        if (!form || !submitButton) {
            return;
        }

        bindAlertDismiss(root, '[data-admin-profile-alert-close="true"]');

        const onSubmit = function (event) {
            event.preventDefault();
            submitForm(root, form, 'admin-profile', alertIds, submitButton, function (payload) {
                window.dispatchEvent(new CustomEvent('admin-profile-updated', {
                    detail: {
                        adminName: payload.adminName
                    }
                }));
            });
        };

        form.addEventListener('submit', onSubmit);

        const cleanup = function () {
            form.removeEventListener('submit', onSubmit);
            delete root.dataset.adminProfileInitialized;
        };

        if (typeof window.__registerAdminEmbeddedCleanup === 'function') {
            window.__registerAdminEmbeddedCleanup(cleanup);
        }
    };

    window.initAdminWorkspaceSettingsModule = function initAdminWorkspaceSettingsModule(root) {
        if (!root || root.dataset.adminWorkspaceInitialized === 'true') {
            return;
        }

        root.dataset.adminWorkspaceInitialized = 'true';
        const form = root.querySelector('#adminWorkspaceSettingsForm');
        const submitButton = root.querySelector('[data-admin-workspace-submit="true"]');
        const alertIds = {
            success: 'adminWorkspaceSuccessAlert',
            error: 'adminWorkspaceErrorAlert',
            successMessage: 'adminWorkspaceSuccessMessage',
            errorMessage: 'adminWorkspaceErrorMessage'
        };

        if (!form || !submitButton) {
            return;
        }

        bindAlertDismiss(root, '[data-admin-workspace-alert-close="true"]');

        const onSubmit = function (event) {
            event.preventDefault();
            submitForm(root, form, 'admin-workspace', alertIds, submitButton, function (payload) {
                window.dispatchEvent(new CustomEvent('admin-workspace-updated', {
                    detail: payload.workspaceSettings || {}
                }));
            });
        };

        form.addEventListener('submit', onSubmit);

        const cleanup = function () {
            form.removeEventListener('submit', onSubmit);
            delete root.dataset.adminWorkspaceInitialized;
        };

        if (typeof window.__registerAdminEmbeddedCleanup === 'function') {
            window.__registerAdminEmbeddedCleanup(cleanup);
        }
    };
})();
