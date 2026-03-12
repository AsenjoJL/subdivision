(function () {
    function setButtonLoading(button, isLoading, loadingText) {
        if (!button) {
            return;
        }

        if (!button.dataset.defaultIcon) {
            const icon = button.querySelector('i');
            const label = button.querySelector('span');
            button.dataset.defaultIcon = icon ? icon.className : '';
            button.dataset.defaultLabel = label ? label.textContent.trim() : button.textContent.trim();
        }

        const icon = button.querySelector('i');
        const label = button.querySelector('span');

        button.disabled = isLoading;
        button.classList.toggle('is-loading', isLoading);

        if (icon) {
            icon.className = isLoading ? 'fas fa-spinner' : (button.dataset.defaultIcon || 'fas fa-circle-notch');
        }

        if (label) {
            label.textContent = isLoading ? (loadingText || 'Processing...') : (button.dataset.defaultLabel || label.textContent);
        }
    }

    function setAlert(root, type, message) {
        const success = root.querySelector('[data-admin-vehicle-success]');
        const error = root.querySelector('[data-admin-vehicle-error]');
        const target = type === 'success' ? success : error;
        const other = type === 'success' ? error : success;

        [success, error].forEach((node) => {
            if (!node) {
                return;
            }

            window.clearTimeout(node.__timerId);
        });

        if (other) {
            other.classList.add('admin-vehicles-hidden');
        }

        if (!target) {
            return;
        }

        const text = target.querySelector('span');
        if (text) {
            text.textContent = message || '';
        }

        if (!message) {
            target.classList.add('admin-vehicles-hidden');
            return;
        }

        target.classList.remove('admin-vehicles-hidden');
        target.__timerId = window.setTimeout(function () {
            target.classList.add('admin-vehicles-hidden');
        }, 3200);
    }

    function openModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.remove('admin-vehicles-hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('admin-vehicles-modal-open');
    }

    function closeModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.add('admin-vehicles-hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('admin-vehicles-modal-open');
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-admin-vehicle-search]')?.value || '').trim().toLowerCase();
        const status = root.querySelector('[data-admin-vehicle-filter]')?.value || 'All';

        root.querySelectorAll('[data-admin-vehicle-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const cardStatus = card.getAttribute('data-status') || '';
            const visible = (!query || searchValue.includes(query)) && (status === 'All' || status === cardStatus);
            card.classList.toggle('admin-vehicles-hidden', !visible);
        });
    }

    async function refreshList(root) {
        const host = root.querySelector('[data-admin-vehicle-list-host]');
        const loadUrl = root.getAttribute('data-load-url');
        if (!host || !loadUrl) {
            return;
        }

        host.classList.add('is-loading');
        try {
            const response = await fetch(loadUrl, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) {
                throw new Error('Unable to refresh vehicle registrations.');
            }

            host.innerHTML = await response.text();
            applyFilters(root);
        } finally {
            host.classList.remove('is-loading');
        }
    }

    async function executeAction(root, url, payload, successMessage, button, loadingText) {
        const formData = new FormData();
        Object.entries(payload).forEach(([key, value]) => {
            formData.append(key, value ?? '');
        });

        setButtonLoading(button, true, loadingText);

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: formData
            });
            const result = await response.json();

            if (!response.ok || !result.success) {
                throw new Error(result.message || 'Unable to update this vehicle registration.');
            }

            setAlert(root, 'success', result.message || successMessage);
            await refreshList(root);
        } finally {
            setButtonLoading(button, false);
        }
    }

    window.initializeAdminVehiclesModule = function initializeAdminVehiclesModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        const detailsModal = root.querySelector('[data-admin-vehicle-details-modal]');
        const statusModal = root.querySelector('[data-admin-vehicle-status-modal]');
        const statusForm = root.querySelector('[data-admin-vehicle-status-form]');
        const expiryField = root.querySelector('[data-admin-vehicle-expiry-field]');

        root.querySelector('[data-admin-vehicle-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        root.querySelector('[data-admin-vehicle-filter]')?.addEventListener('change', function () {
            applyFilters(root);
        });

        root.addEventListener('click', async function (event) {
            const closeDetails = event.target.closest('[data-admin-vehicle-close]');
            if (closeDetails) {
                closeModal(detailsModal);
                return;
            }

            const closeStatus = event.target.closest('[data-admin-vehicle-status-close]');
            if (closeStatus) {
                closeModal(statusModal);
                return;
            }

            const viewButton = event.target.closest('[data-admin-vehicle-view]');
            if (viewButton) {
                try {
                    const detailsUrl = root.getAttribute('data-details-url');
                    const separator = detailsUrl.includes('?') ? '&' : '?';
                    const response = await fetch(`${detailsUrl}${separator}id=${encodeURIComponent(viewButton.getAttribute('data-vehicle-id') || '')}`, {
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    });
                    const result = await response.json();

                    if (!response.ok || !result.success) {
                        throw new Error(result.message || 'Unable to load vehicle registration details.');
                    }

                    Object.entries(result.vehicle || {}).forEach(([key, value]) => {
                        const node = detailsModal.querySelector(`[data-vehicle-detail="${key}"]`);
                        if (node) {
                            node.textContent = value || '-';
                        }
                    });

                    openModal(detailsModal);
                } catch (error) {
                    setAlert(root, 'error', error.message || 'Unable to load vehicle registration details.');
                }
                return;
            }

            const openStatusButton = event.target.closest('[data-admin-vehicle-open-status]');
            if (openStatusButton) {
                const statusValue = openStatusButton.getAttribute('data-vehicle-status') || '';
                statusForm.querySelector('[data-status-field="id"]').value = openStatusButton.getAttribute('data-vehicle-id') || '';
                statusForm.querySelector('[data-status-field="status"]').value = statusValue;
                statusForm.querySelector('[data-status-field="expiryDate"]').value = '';
                statusForm.querySelector('textarea[name="notes"]').value = '';
                root.querySelector('[data-admin-vehicle-status-title]').textContent = openStatusButton.getAttribute('data-vehicle-title') || 'Update vehicle registration';
                expiryField?.classList.toggle('admin-vehicles-hidden', statusValue !== 'Approved');
                openModal(statusModal);
                return;
            }

            const deleteButton = event.target.closest('[data-admin-vehicle-delete]');
            if (deleteButton) {
                try {
                    await executeAction(
                        root,
                        root.getAttribute('data-delete-url') || '',
                        { id: deleteButton.getAttribute('data-vehicle-id') || '' },
                        'Vehicle registration deleted successfully.',
                        deleteButton,
                        'Deleting...'
                    );
                } catch (error) {
                    setAlert(root, 'error', error.message || 'Unable to delete vehicle registration.');
                }
            }
        });

        statusForm?.addEventListener('submit', async function (event) {
            event.preventDefault();
            const submit = statusForm.querySelector('[data-admin-vehicle-status-submit]');
            const statusValue = statusForm.querySelector('[data-status-field="status"]').value;
            const loadingText = statusValue === 'Approved' ? 'Approving...' : 'Rejecting...';

            try {
                await executeAction(
                    root,
                    root.getAttribute('data-status-url') || '',
                    {
                        id: statusForm.querySelector('[data-status-field="id"]').value,
                        status: statusValue,
                        expiryDate: statusForm.querySelector('[data-status-field="expiryDate"]').value,
                        notes: statusForm.querySelector('textarea[name="notes"]').value
                    },
                    'Vehicle registration updated successfully.',
                    submit,
                    loadingText
                );
                closeModal(statusModal);
            } catch (error) {
                setAlert(root, 'error', error.message || 'Unable to update vehicle registration.');
            }
        });

        [detailsModal, statusModal].forEach((modal) => {
            modal?.addEventListener('click', function (event) {
                if (event.target === modal || event.target.hasAttribute('data-admin-vehicle-close') || event.target.hasAttribute('data-admin-vehicle-status-close')) {
                    closeModal(modal);
                }
            });
        });

        applyFilters(root);
    };
})();
