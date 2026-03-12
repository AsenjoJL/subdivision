(function () {
    function setAlert(root, element, message) {
        if (!element) {
            return;
        }

        const textNode = element.querySelector('span');
        if (textNode) {
            textNode.textContent = message || '';
        }

        element.classList.toggle('admin-complaints-hidden', !message);
        if (message) {
            window.clearTimeout(element.__timerId);
            element.__timerId = window.setTimeout(function () {
                element.classList.add('admin-complaints-hidden');
            }, 3200);
        }
    }

    async function refreshComplaints(root) {
        const listHost = root.querySelector('[data-admin-complaint-list]');
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
                throw new Error('Unable to refresh complaints.');
            }

            listHost.innerHTML = await response.text();
            applyFilters(root);
        } finally {
            listHost.classList.remove('is-loading');
        }
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-admin-complaint-search]')?.value || '').trim().toLowerCase();
        const status = root.querySelector('[data-admin-complaint-status]')?.value || 'All';
        const cards = Array.from(root.querySelectorAll('[data-admin-complaint-card]'));
        let visible = 0;

        cards.forEach((card) => {
            const matchesSearch = !query || (card.getAttribute('data-search') || '').toLowerCase().includes(query);
            const matchesStatus = status === 'All' || card.getAttribute('data-status') === status;
            const shouldShow = matchesSearch && matchesStatus;
            card.classList.toggle('admin-complaints-hidden', !shouldShow);
            if (shouldShow) {
                visible += 1;
            }
        });

        root.querySelectorAll('[data-admin-complaints-visible]').forEach((node) => {
            node.textContent = visible.toString();
        });
    }

    function openModal(modal) {
        if (!modal) {
            return;
        }

        modal.hidden = false;
        modal.classList.add('is-open');
    }

    function closeModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.remove('is-open');
        modal.hidden = true;
    }

    window.initializeAdminComplaintsModule = function initializeAdminComplaintsModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        const successAlert = root.querySelector('[data-admin-complaint-success]');
        const errorAlert = root.querySelector('[data-admin-complaint-error]');
        const detailsModal = root.querySelector('[data-admin-complaint-details-modal]');
        const updateModal = root.querySelector('[data-admin-complaint-update-modal]');
        const updateForm = root.querySelector('[data-admin-complaint-update-form]');

        root.addEventListener('click', async function (event) {
            const closeButton = event.target.closest('[data-admin-complaint-close]');
            if (closeButton) {
                closeModal(detailsModal);
                closeModal(updateModal);
                return;
            }

            const viewButton = event.target.closest('[data-admin-complaint-view]');
            if (viewButton) {
                setAlert(root, successAlert, '');
                setAlert(root, errorAlert, '');
                try {
                    const response = await fetch(viewButton.getAttribute('data-details-url') || '', {
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    });
                    const result = await response.json();
                    if (!response.ok || !result.success) {
                        throw new Error(result.message || 'Unable to load complaint details.');
                    }

                    Object.entries(result.complaint || {}).forEach(([key, value]) => {
                        const node = detailsModal.querySelector(`[data-detail="${key}"]`);
                        if (node) {
                            node.textContent = value || '-';
                        }
                    });

                    openModal(detailsModal);
                } catch (error) {
                    setAlert(root, errorAlert, error.message || 'Unable to load complaint details.');
                }
                return;
            }

            const editButton = event.target.closest('[data-admin-complaint-edit]');
            if (editButton) {
                updateForm.querySelector('[data-update-field="Id"]').value = editButton.getAttribute('data-complaint-id') || '';
                updateForm.querySelector('[data-update-field="Status"]').value = editButton.getAttribute('data-complaint-status') || 'Submitted';
                updateForm.querySelector('[data-update-field="Response"]').value = editButton.getAttribute('data-complaint-response') || '';
                updateForm.querySelector('[data-update-field="ResolutionNotes"]').value = editButton.getAttribute('data-complaint-resolution') || '';
                openModal(updateModal);
                return;
            }

            const deleteButton = event.target.closest('[data-admin-complaint-delete]');
            if (deleteButton) {
                if (!window.confirm('Delete this complaint?')) {
                    return;
                }

                try {
                    const formData = new FormData();
                    formData.append('id', deleteButton.getAttribute('data-delete-id') || '');

                    const response = await fetch(deleteButton.getAttribute('data-delete-url') || '', {
                        method: 'POST',
                        body: formData
                    });
                    const result = await response.json();
                    if (!response.ok || !result.success) {
                        throw new Error(result.message || 'Unable to delete complaint.');
                    }

                    await refreshComplaints(root);
                    setAlert(root, successAlert, result.message || 'Complaint deleted successfully.');
                } catch (error) {
                    setAlert(root, errorAlert, error.message || 'Unable to delete complaint.');
                }
            }
        });

        updateForm?.addEventListener('submit', async function (event) {
            event.preventDefault();
            const submitButton = updateForm.querySelector('[data-admin-complaint-submit]');
            submitButton.disabled = true;
            setAlert(root, successAlert, '');
            setAlert(root, errorAlert, '');

            try {
                const response = await fetch(updateForm.action, {
                    method: 'POST',
                    body: new FormData(updateForm)
                });
                const result = await response.json();
                if (!response.ok || !result.success) {
                    throw new Error(result.message || 'Unable to update complaint.');
                }

                await refreshComplaints(root);
                closeModal(updateModal);
                setAlert(root, successAlert, result.message || 'Complaint updated successfully.');
            } catch (error) {
                setAlert(root, errorAlert, error.message || 'Unable to update complaint.');
            } finally {
                submitButton.disabled = false;
            }
        });

        root.querySelector('[data-admin-complaint-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        root.querySelector('[data-admin-complaint-status]')?.addEventListener('change', function () {
            applyFilters(root);
        });

        root.querySelectorAll('.admin-complaints-modal').forEach((modal) => {
            modal.addEventListener('click', function (event) {
                if (event.target === modal) {
                    closeModal(modal);
                }
            });
        });

        applyFilters(root);
    };
})();
