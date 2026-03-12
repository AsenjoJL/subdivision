(function () {
    function getStaffModuleInitializers() {
        window.staffDashboardModuleInitializers = window.staffDashboardModuleInitializers || {};
        return window.staffDashboardModuleInitializers;
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

        element.classList.toggle('staff-profile-hidden', !message);
        if (message) {
            window.clearTimeout(element.__timerId);
            element.__timerId = window.setTimeout(function () {
                element.classList.add('staff-profile-hidden');
            }, 3600);
        }
    }

    function clearValidation(root) {
        root.querySelectorAll('.staff-profile-field').forEach((field) => field.classList.remove('is-invalid'));
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

            root.querySelector(`[data-field="${key}"]`)?.closest('.staff-profile-field')?.classList.add('is-invalid');
        });
    }

    function updateDisplayedName(name) {
        if (!name) {
            return;
        }

        document.querySelectorAll('[data-staff-display-name]').forEach((node) => {
            node.textContent = name;
        });
    }

    function updateDisplayedImage(imagePath) {
        if (!imagePath) {
            return;
        }

        document.querySelectorAll('[data-staff-profile-image], [data-staff-profile-preview]').forEach((node) => {
            node.src = imagePath;
        });
    }

    const initializeStaffProfile = function (contentRoot) {
        const root = contentRoot.querySelector('[data-staff-module="profile"]')
            || (contentRoot.matches && contentRoot.matches('[data-staff-module="profile"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        const successAlert = root.querySelector('[data-staff-profile-success]');
        const errorAlert = root.querySelector('[data-staff-profile-error]');
        const resetLinkWrap = root.querySelector('[data-staff-profile-reset-link-wrap]');
        const resetLinkField = root.querySelector('[data-staff-profile-reset-link]');
        const copyLinkButton = root.querySelector('[data-staff-profile-copy-link]');
        const openLinkButton = root.querySelector('[data-staff-profile-open-link]');
        const uploadTrigger = root.querySelector('[data-staff-profile-upload-trigger]');
        const onProfileImageUploaded = function (event) {
            const detail = event.detail || {};
            if (detail.imagePath) {
                updateDisplayedImage(detail.imagePath);
            }

            setAlert(successAlert, detail.message || 'Profile picture updated successfully.');
        };
        const onProfileImageError = function (event) {
            setAlert(errorAlert, event.detail?.message || 'Unable to upload the profile picture right now.');
        };

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

        uploadTrigger?.addEventListener('click', function () {
            window.staffDashboardShell?.triggerProfileUpload?.();
        });

        root.addEventListener('submit', async function (event) {
            const profileForm = event.target.closest('[data-staff-profile-form]');
            const resetForm = event.target.closest('[data-staff-profile-reset-form]');

            if (!profileForm && !resetForm) {
                return;
            }

            event.preventDefault();
            clearValidation(root);
            setAlert(successAlert, '');
            setAlert(errorAlert, '');

            if (profileForm) {
                const submitButton = profileForm.querySelector('[data-staff-profile-submit]');
                submitButton.disabled = true;

                try {
                    const response = await fetch(profileForm.action, {
                        method: 'POST',
                        headers: { 'X-Requested-With': 'XMLHttpRequest' },
                        body: new FormData(profileForm)
                    });

                    const result = await response.json();
                    if (!response.ok || !result.success) {
                        applyValidation(root, result.validationErrors);
                        setAlert(errorAlert, result.message || 'Unable to update the staff profile.');
                        return;
                    }

                    updateDisplayedName(result.staffName);

                    if (window.staffDashboardShell?.invalidate) {
                        if (result.dashboardUrl) {
                            window.staffDashboardShell.invalidate(result.dashboardUrl);
                        }
                        if (result.profileUrl) {
                            window.staffDashboardShell.invalidate(result.profileUrl);
                        }
                    }

                    document.dispatchEvent(new CustomEvent('staff-profile-updated', {
                        detail: result
                    }));

                    setAlert(successAlert, result.message || 'Staff profile updated successfully.');
                } catch (error) {
                    setAlert(errorAlert, 'Unable to update the staff profile right now.');
                } finally {
                    submitButton.disabled = false;
                }

                return;
            }

            const resetButton = resetForm.querySelector('[data-staff-profile-reset-submit]');
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
                    resetLinkWrap.classList.remove('staff-profile-hidden');
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

        document.addEventListener('staff-profile-image:uploaded', onProfileImageUploaded);
        document.addEventListener('staff-profile-image:error', onProfileImageError);

        if (typeof window.__registerStaffEmbeddedCleanup === 'function') {
            window.__registerStaffEmbeddedCleanup(function () {
                document.removeEventListener('staff-profile-image:uploaded', onProfileImageUploaded);
                document.removeEventListener('staff-profile-image:error', onProfileImageError);
            });
        }
    };

    window.initializeStaffProfile = initializeStaffProfile;
    getStaffModuleInitializers().profile = initializeStaffProfile;
})();
