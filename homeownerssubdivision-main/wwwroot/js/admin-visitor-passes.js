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
            icon.className = isLoading
                ? 'fas fa-spinner'
                : (button.dataset.defaultIcon || 'fas fa-circle-notch');
        }

        if (label) {
            label.textContent = isLoading
                ? (loadingText || 'Processing...')
                : (button.dataset.defaultLabel || label.textContent);
        }
    }

    function setAlert(root, type, message) {
        const success = root.querySelector('[data-admin-visitor-pass-success]');
        const error = root.querySelector('[data-admin-visitor-pass-error]');
        const target = type === 'success' ? success : error;
        const other = type === 'success' ? error : success;

        [success, error].forEach((node) => {
            if (!node) {
                return;
            }
            window.clearTimeout(node.__timerId);
        });

        if (other) {
            other.classList.add('admin-visitor-passes-hidden');
        }

        if (!target) {
            return;
        }

        const text = target.querySelector('span');
        if (text) {
            text.textContent = message || '';
        }

        if (!message) {
            target.classList.add('admin-visitor-passes-hidden');
            return;
        }

        target.classList.remove('admin-visitor-passes-hidden');
        target.__timerId = window.setTimeout(function () {
            target.classList.add('admin-visitor-passes-hidden');
        }, 3200);
    }

    function openModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.remove('admin-visitor-passes-hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('admin-visitor-passes-modal-open');
    }

    function closeModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.add('admin-visitor-passes-hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('admin-visitor-passes-modal-open');
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-admin-visitor-pass-search]')?.value || '').trim().toLowerCase();
        const status = root.querySelector('[data-admin-visitor-pass-filter]')?.value || 'All';

        root.querySelectorAll('[data-admin-visitor-pass-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const cardStatus = card.getAttribute('data-status') || '';
            const visible = (!query || searchValue.includes(query)) && (status === 'All' || status === cardStatus);
            card.classList.toggle('admin-visitor-passes-hidden', !visible);
        });
    }

    async function refreshList(root) {
        const host = root.querySelector('[data-admin-visitor-pass-list-host]');
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
                throw new Error('Unable to refresh visitor passes.');
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
                throw new Error(result.message || 'Unable to update this visitor pass.');
            }

            setAlert(root, 'success', result.message || successMessage);
            await refreshList(root);
        } finally {
            setButtonLoading(button, false);
        }
    }

    window.initializeAdminVisitorPassesModule = function initializeAdminVisitorPassesModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        const detailsModal = root.querySelector('[data-admin-visitor-pass-details-modal]');
        const statusModal = root.querySelector('[data-admin-visitor-pass-status-modal]');
        const statusForm = root.querySelector('[data-admin-visitor-pass-status-form]');

        root.querySelector('[data-admin-visitor-pass-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        root.querySelector('[data-admin-visitor-pass-filter]')?.addEventListener('change', function () {
            applyFilters(root);
        });

        root.addEventListener('click', async function (event) {
            const closeDetails = event.target.closest('[data-admin-visitor-pass-close]');
            if (closeDetails) {
                closeModal(detailsModal);
                return;
            }

            const closeStatus = event.target.closest('[data-admin-visitor-pass-status-close]');
            if (closeStatus) {
                closeModal(statusModal);
                return;
            }

            const viewButton = event.target.closest('[data-admin-visitor-pass-view]');
            if (viewButton) {
                try {
                    const detailsUrl = root.getAttribute('data-details-url');
                    const separator = detailsUrl.includes('?') ? '&' : '?';
                    const response = await fetch(`${detailsUrl}${separator}id=${encodeURIComponent(viewButton.getAttribute('data-pass-id') || '')}`, {
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    });
                    const result = await response.json();

                    if (!response.ok || !result.success) {
                        throw new Error(result.message || 'Unable to load visitor pass details.');
                    }

                    Object.entries(result.pass || {}).forEach(([key, value]) => {
                        const node = detailsModal.querySelector(`[data-pass-detail="${key}"]`);
                        if (node) {
                            node.textContent = value || '-';
                        }
                    });

                    openModal(detailsModal);
                } catch (error) {
                    setAlert(root, 'error', error.message || 'Unable to load visitor pass details.');
                }
                return;
            }

            const openStatusButton = event.target.closest('[data-admin-visitor-pass-open-status]');
            if (openStatusButton) {
                statusForm.querySelector('[data-status-field="id"]').value = openStatusButton.getAttribute('data-pass-id') || '';
                statusForm.querySelector('[data-status-field="status"]').value = openStatusButton.getAttribute('data-pass-status') || '';
                statusForm.querySelector('textarea[name="notes"]').value = '';
                root.querySelector('[data-admin-visitor-pass-status-title]').textContent = openStatusButton.getAttribute('data-pass-title') || 'Update visitor pass';
                openModal(statusModal);
                return;
            }

            const checkInButton = event.target.closest('[data-admin-visitor-pass-checkin]');
            if (checkInButton) {
                try {
                    await executeAction(
                        root,
                        root.getAttribute('data-checkin-url') || '',
                        { id: checkInButton.getAttribute('data-pass-id') || '' },
                        'Visitor checked in successfully.',
                        checkInButton,
                        'Checking in...'
                    );
                } catch (error) {
                    setAlert(root, 'error', error.message || 'Unable to check in visitor.');
                }
                return;
            }

            const checkOutButton = event.target.closest('[data-admin-visitor-pass-checkout]');
            if (checkOutButton) {
                try {
                    await executeAction(
                        root,
                        root.getAttribute('data-checkout-url') || '',
                        { id: checkOutButton.getAttribute('data-pass-id') || '' },
                        'Visitor checked out successfully.',
                        checkOutButton,
                        'Checking out...'
                    );
                } catch (error) {
                    setAlert(root, 'error', error.message || 'Unable to check out visitor.');
                }
            }
        });

        statusForm?.addEventListener('submit', async function (event) {
            event.preventDefault();
            const submit = statusForm.querySelector('[data-admin-visitor-pass-status-submit]');
            const statusValue = statusForm.querySelector('[data-status-field="status"]').value;
            const loadingText = statusValue === 'Approved' ? 'Approving...' : 'Rejecting...';

            try {
                await executeAction(
                    root,
                    root.getAttribute('data-status-url') || '',
                    {
                        id: statusForm.querySelector('[data-status-field="id"]').value,
                        status: statusValue,
                        notes: statusForm.querySelector('textarea[name="notes"]').value
                    },
                    'Visitor pass updated successfully.',
                    submit,
                    loadingText
                );
                closeModal(statusModal);
            } catch (error) {
                setAlert(root, 'error', error.message || 'Unable to update visitor pass.');
            }
        });

        [detailsModal, statusModal].forEach((modal) => {
            modal?.addEventListener('click', function (event) {
                if (event.target === modal || event.target.hasAttribute('data-admin-visitor-pass-close') || event.target.hasAttribute('data-admin-visitor-pass-status-close')) {
                    closeModal(modal);
                }
            });
        });

        applyFilters(root);
    };
})();
