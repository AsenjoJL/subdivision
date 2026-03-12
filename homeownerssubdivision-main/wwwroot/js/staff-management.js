(function () {
    function getStaffModuleInitializers() {
        window.staffDashboardModuleInitializers = window.staffDashboardModuleInitializers || {};
        return window.staffDashboardModuleInitializers;
    }

    const initializeStaffManagement = function (contentRoot) {
        const root = contentRoot.querySelector('[data-staff-view="management"]')
            || (contentRoot.matches && contentRoot.matches('[data-staff-view="management"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('click', async function (event) {
            const completeButton = event.target.closest('[data-complete-request]');
            if (!completeButton) {
                return;
            }

            event.preventDefault();

            if (!window.confirm('Mark this request as completed?')) {
                return;
            }

            completeButton.disabled = true;

            try {
                const formData = new FormData();
                formData.append('requestId', completeButton.getAttribute('data-request-id') || '');
                formData.append('status', 'Completed');

                const token = root.querySelector('input[name="__RequestVerificationToken"]');
                if (token) {
                    formData.append('__RequestVerificationToken', token.value);
                }

                const response = await fetch(completeButton.getAttribute('data-update-url') || '', {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: formData
                });

                const result = await response.json();
                if (!response.ok || !result.success) {
                    alert(result.message || 'Unable to update request.');
                    return;
                }

                const shell = window.staffDashboardShell;
                if (shell?.urls?.management) {
                    shell.invalidate(shell.urls.management);
                }
                if (shell?.urls?.dashboard) {
                    shell.invalidate(shell.urls.dashboard);
                }

                await shell?.loadSection(shell.urls.management, 'Task Management', { forceRefresh: true });
            } catch (error) {
                alert('Unable to update request right now.');
            } finally {
                completeButton.disabled = false;
            }
        });
    };

    window.initializeStaffManagement = initializeStaffManagement;
    getStaffModuleInitializers().management = initializeStaffManagement;
})();
