(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function toArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function setAlert(element, message) {
        if (!element) {
            return;
        }

        const textNode = element.querySelector('span');
        if (textNode) {
            textNode.textContent = message || '';
        }

        element.classList.toggle('homeowner-settings-hidden', !message);
        if (message) {
            window.clearTimeout(element.__timerId);
            element.__timerId = window.setTimeout(function () {
                element.classList.add('homeowner-settings-hidden');
            }, 3600);
        }
    }

    function clearValidation(root) {
        root.querySelectorAll('.homeowner-settings-field').forEach((field) => field.classList.remove('is-invalid'));
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

            root.querySelector(`[data-field="${key}"]`)?.closest('.homeowner-settings-field')?.classList.add('is-invalid');
        });
    }

    function updateDisplayedName(name) {
        if (!name) {
            return;
        }

        document.querySelectorAll('[data-homeowner-display-name]').forEach((node) => {
            node.textContent = name;
        });
    }

    const initializeHomeownerSettings = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="settings"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="settings"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        const successAlert = root.querySelector('[data-settings-success]');
        const errorAlert = root.querySelector('[data-settings-error]');
        const resetLinkWrap = root.querySelector('[data-settings-reset-link-wrap]');
        const resetLinkField = root.querySelector('[data-settings-reset-link]');
        const copyLinkButton = root.querySelector('[data-settings-copy-link]');
        const openLinkButton = root.querySelector('[data-settings-open-link]');
        const onProfileUploadSuccess = function (event) {
            const detail = event.detail || {};
            if (detail.imagePath) {
                root.querySelectorAll('[data-homeowner-profile-image]').forEach((imageNode) => {
                    imageNode.src = detail.imagePath;
                });
            }

            setAlert(successAlert, detail.message || 'Profile picture updated successfully.');
        };
        const onProfileUploadError = function (event) {
            setAlert(errorAlert, event.detail?.message || 'Unable to upload your profile picture right now.');
        };

        document.addEventListener('homeowner-profile-image:uploaded', onProfileUploadSuccess);
        document.addEventListener('homeowner-profile-image:error', onProfileUploadError);

        if (typeof window.__registerHomeownerEmbeddedCleanup === 'function') {
            window.__registerHomeownerEmbeddedCleanup(function () {
                document.removeEventListener('homeowner-profile-image:uploaded', onProfileUploadSuccess);
                document.removeEventListener('homeowner-profile-image:error', onProfileUploadError);
            });
        }

        copyLinkButton?.addEventListener('click', async function () {
            const value = resetLinkField?.value?.trim();
            if (!value) {
                setAlert(errorAlert, 'No password reset link is available yet.');
                return;
            }

            try {
                await navigator.clipboard.writeText(value);
                setAlert(successAlert, 'Password reset link copied successfully.');
            } catch (error) {
                setAlert(errorAlert, 'Unable to copy the password reset link right now.');
            }
        });

        root.addEventListener('submit', async function (event) {
            const form = event.target.closest('[data-settings-form]');
            const resetForm = event.target.closest('[data-settings-reset-form]');

            if (!form && !resetForm) {
                return;
            }

            event.preventDefault();
            clearValidation(root);
            setAlert(successAlert, '');
            setAlert(errorAlert, '');

            if (form) {
                const submitButton = form.querySelector('[data-settings-submit]');
                submitButton.disabled = true;

                try {
                    const response = await fetch(form.action, {
                        method: 'POST',
                        headers: { 'X-Requested-With': 'XMLHttpRequest' },
                        body: new FormData(form)
                    });

                    const result = await response.json();
                    if (!response.ok || !result.success) {
                        applyValidation(root, result.validationErrors);
                        setAlert(errorAlert, result.message || 'Unable to update your settings.');
                        return;
                    }

                    updateDisplayedName(result.displayName);

                    if (window.homeownerDashboardShell?.invalidate) {
                        if (result.homeUrl) {
                            window.homeownerDashboardShell.invalidate(result.homeUrl);
                        }
                        if (result.settingsUrl) {
                            window.homeownerDashboardShell.invalidate(result.settingsUrl);
                        }
                    }

                    setAlert(successAlert, result.message || 'Settings updated successfully.');
                } catch (error) {
                    setAlert(errorAlert, 'Unable to update your settings right now. Please try again.');
                } finally {
                    submitButton.disabled = false;
                }

                return;
            }

            const resetButton = resetForm.querySelector('[data-settings-reset-submit]');
            resetButton.disabled = true;

            try {
                const response = await fetch(resetForm.action, {
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    body: new FormData(resetForm)
                });

                const result = await response.json();
                if (!response.ok || !result.success) {
                    setAlert(errorAlert, result.message || 'Unable to generate a password reset link.');
                    return;
                }

                if (resetLinkWrap && resetLinkField && result.resetLink) {
                    resetLinkField.value = result.resetLink;
                    resetLinkWrap.classList.remove('homeowner-settings-hidden');
                    if (openLinkButton) {
                        openLinkButton.href = result.resetLink;
                    }
                }

                setAlert(successAlert, result.message || 'Password reset link generated successfully.');
            } catch (error) {
                setAlert(errorAlert, 'Unable to generate a password reset link right now.');
            } finally {
                resetButton.disabled = false;
            }
        });
    };

    window.initializeHomeownerSettings = initializeHomeownerSettings;
    getDashboardModuleInitializers().settings = initializeHomeownerSettings;
})();
