(function () {
    function setAlert(root, message) {
        const alert = root.querySelector('[data-admin-gate-error]');
        if (!alert) {
            return;
        }

        const text = alert.querySelector('span');
        if (text) {
            text.textContent = message || '';
        }

        alert.classList.toggle('admin-gate-hidden', !message);
    }

    function toggleModal(root, show) {
        const modal = root.querySelector('[data-admin-gate-modal]');
        if (!modal) {
            return;
        }

        const shouldShow = typeof show === 'boolean'
            ? show
            : modal.hasAttribute('hidden');

        modal.hidden = !shouldShow;
        modal.classList.toggle('admin-gate-hidden', !shouldShow);
        document.body.classList.toggle('admin-gate-modal-open', shouldShow);
    }

    function fillDetails(root, card) {
        const mapping = {
            name: 'logName',
            user: 'logUser',
            access: 'logAccess',
            time: 'logTime',
            plate: 'logPlate',
            gate: 'logGate',
            verified: 'logVerified',
            notes: 'logNotes'
        };

        Object.entries(mapping).forEach(([target, source]) => {
            const node = root.querySelector(`[data-admin-gate-detail="${target}"]`);
            if (node) {
                node.textContent = card.dataset[source] || 'N/A';
            }
        });
    }

    function buildQueryString(root) {
        const params = new URLSearchParams();
        const search = root.querySelector('[data-admin-gate-search]')?.value?.trim();
        const userType = root.querySelector('[data-admin-gate-user-type]')?.value || '';
        const accessType = root.querySelector('[data-admin-gate-access-type]')?.value || '';
        const startDate = root.querySelector('[data-admin-gate-start-date]')?.value || '';
        const endDate = root.querySelector('[data-admin-gate-end-date]')?.value || '';

        if (search) {
            params.set('search', search);
        }
        if (userType) {
            params.set('userType', userType);
        }
        if (accessType) {
            params.set('accessType', accessType);
        }
        if (startDate) {
            params.set('startDate', startDate);
        }
        if (endDate) {
            params.set('endDate', endDate);
        }

        return params.toString();
    }

    async function refreshModule(root) {
        const toolbar = root.querySelector('[data-admin-gate-toolbar]');
        const loadUrl = toolbar?.getAttribute('data-load-url');
        if (!loadUrl) {
            return;
        }

        root.classList.add('is-loading');
        setAlert(root, '');

        try {
            const query = buildQueryString(root);
            const response = await fetch(query ? `${loadUrl}?${query}` : loadUrl, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error('Unable to load gate access logs right now.');
            }

            const html = await response.text();
            const wrapper = document.createElement('div');
            wrapper.innerHTML = html.trim();
            const nextRoot = wrapper.querySelector('[data-admin-gate-module]');
            if (!nextRoot) {
                throw new Error('Gate access markup was invalid.');
            }

            root.replaceWith(nextRoot);
            if (typeof window.initializeAdminGateAccessModule === 'function') {
                window.initializeAdminGateAccessModule(nextRoot);
            }
        } catch (error) {
            setAlert(root, error.message || 'Unable to load gate access logs right now.');
        } finally {
            root.classList.remove('is-loading');
        }
    }

    function resetFilters(root) {
        root.querySelector('[data-admin-gate-search]').value = '';
        root.querySelector('[data-admin-gate-user-type]').value = '';
        root.querySelector('[data-admin-gate-access-type]').value = '';
    }

    window.initializeAdminGateAccessModule = function initializeAdminGateAccessModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';
        root.__adminGateEscapeHandler = function (event) {
            if (event.key === 'Escape') {
                toggleModal(root, false);
            }
        };
        document.addEventListener('keydown', root.__adminGateEscapeHandler);

        root.addEventListener('click', function (event) {
            const applyButton = event.target.closest('[data-admin-gate-apply]');
            if (applyButton) {
                refreshModule(root);
                return;
            }

            const resetButton = event.target.closest('[data-admin-gate-reset]');
            if (resetButton) {
                resetFilters(root);
                refreshModule(root);
                return;
            }

            const viewButton = event.target.closest('[data-admin-gate-view]');
            if (viewButton) {
                const card = viewButton.closest('[data-admin-gate-card]');
                if (card) {
                    fillDetails(root, card);
                    toggleModal(root, true);
                }
                return;
            }

            const closeButton = event.target.closest('[data-admin-gate-close]');
            if (closeButton) {
                toggleModal(root, false);
            }
        });

        root.addEventListener('keydown', function (event) {
            if (event.target.matches('[data-admin-gate-search]') && event.key === 'Enter') {
                event.preventDefault();
                refreshModule(root);
            }
        });

        if (typeof window.__registerAdminEmbeddedCleanup === 'function') {
            window.__registerAdminEmbeddedCleanup(function () {
                document.removeEventListener('keydown', root.__adminGateEscapeHandler);
                document.body.classList.remove('admin-gate-modal-open');
            });
        }
    };
})();
