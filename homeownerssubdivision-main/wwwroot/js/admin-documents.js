(function () {
    function getAlertTimers(root) {
        if (!root.__adminDocumentsAlertTimers) {
            root.__adminDocumentsAlertTimers = new Map();
        }

        return root.__adminDocumentsAlertTimers;
    }

    function clearAlertTimer(root, element) {
        const timers = getAlertTimers(root);
        const timerId = timers.get(element);
        if (timerId) {
            window.clearTimeout(timerId);
            timers.delete(element);
        }
    }

    function setAlert(root, element, message, options) {
        if (!element) {
            return;
        }

        clearAlertTimer(root, element);

        const textNode = element.querySelector('span');
        if (textNode) {
            textNode.textContent = message || '';
        }

        element.classList.toggle('admin-documents-hidden', !message);

        const settings = options || {};
        if (message && settings.autoHide !== false) {
            const timers = getAlertTimers(root);
            const timerId = window.setTimeout(function () {
                element.classList.add('admin-documents-hidden');
                timers.delete(element);
            }, settings.duration || 3200);
            timers.set(element, timerId);
        }
    }

    function toggleUploadPanel(panel, show) {
        if (!panel) {
            return;
        }

        const shouldShow = typeof show === 'boolean'
            ? show
            : panel.hasAttribute('hidden');

        panel.hidden = !shouldShow;
        panel.classList.toggle('admin-documents-hidden', !shouldShow);

        if (shouldShow) {
            panel.querySelector('input, select, textarea')?.focus();
        }
    }

    function updateVisibleCount(root) {
        const visibleCount = root.querySelectorAll('[data-doc-admin-card]:not(.admin-documents-hidden)').length;
        root.querySelectorAll('[data-doc-admin-visible-count]').forEach((node) => {
            node.textContent = visibleCount.toString();
        });
    }

    function applySearchFilter(root) {
        const query = (root.querySelector('[data-doc-admin-search]')?.value || '').trim().toLowerCase();
        root.querySelectorAll('[data-doc-admin-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const matches = !query || searchValue.includes(query);
            card.classList.toggle('admin-documents-hidden', !matches);
        });

        const emptyState = root.querySelector('[data-doc-admin-empty]');
        const anyVisible = root.querySelector('[data-doc-admin-card]:not(.admin-documents-hidden)');
        emptyState?.classList.toggle('admin-documents-hidden', Boolean(anyVisible));
        updateVisibleCount(root);
    }

    async function refreshDocumentCards(root) {
        const listHost = root.querySelector('[data-doc-admin-list-host]');
        const loadUrl = listHost?.getAttribute('data-load-url');
        if (!listHost || !loadUrl) {
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
                throw new Error('Failed to refresh documents.');
            }

            listHost.innerHTML = await response.text();
            applySearchFilter(root);
        } finally {
            listHost.classList.remove('is-loading');
        }
    }

    async function uploadDocument(root, form) {
        const successAlert = root.querySelector('[data-doc-admin-success]');
        const errorAlert = root.querySelector('[data-doc-admin-error]');
        const submitButton = form.querySelector('[data-doc-admin-submit]');

        setAlert(root, successAlert, '', { autoHide: false });
        setAlert(root, errorAlert, '', { autoHide: false });

        if (submitButton) {
            submitButton.disabled = true;
        }

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form)
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, errorAlert, result.message || 'Unable to upload document.');
                return;
            }

            await refreshDocumentCards(root);
            form.reset();
            toggleUploadPanel(root.querySelector('[data-doc-admin-upload-panel]'), false);
            setAlert(root, successAlert, result.message || 'Document uploaded successfully!');
        } catch (error) {
            setAlert(root, errorAlert, 'Unable to upload document right now. Please try again.');
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    async function deleteDocument(root, button) {
        const successAlert = root.querySelector('[data-doc-admin-success]');
        const errorAlert = root.querySelector('[data-doc-admin-error]');
        const documentId = button.getAttribute('data-delete-id');
        const deleteUrl = button.getAttribute('data-delete-url');
        if (!documentId || !deleteUrl) {
            return;
        }

        if (!window.confirm('Delete this document?')) {
            return;
        }

        button.disabled = true;
        setAlert(root, successAlert, '', { autoHide: false });
        setAlert(root, errorAlert, '', { autoHide: false });

        try {
            const formData = new FormData();
            formData.append('id', documentId);

            const response = await fetch(deleteUrl, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, errorAlert, result.message || 'Unable to delete document.');
                return;
            }

            await refreshDocumentCards(root);
            setAlert(root, successAlert, result.message || 'Document deleted successfully!');
        } catch (error) {
            setAlert(root, errorAlert, 'Unable to delete document right now. Please try again.');
        } finally {
            button.disabled = false;
        }
    }

    window.initializeAdminDocumentsModule = function initializeAdminDocumentsModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('click', function (event) {
            const uploadToggle = event.target.closest('[data-doc-admin-toggle]');
            if (uploadToggle) {
                toggleUploadPanel(root.querySelector('[data-doc-admin-upload-panel]'));
                return;
            }

            const cancelButton = event.target.closest('[data-doc-admin-cancel]');
            if (cancelButton) {
                toggleUploadPanel(root.querySelector('[data-doc-admin-upload-panel]'), false);
                return;
            }

            const deleteButton = event.target.closest('[data-doc-admin-delete]');
            if (deleteButton) {
                deleteDocument(root, deleteButton);
                return;
            }

            const downloadLink = event.target.closest('[data-doc-admin-download]');
            if (downloadLink) {
                const successAlert = root.querySelector('[data-doc-admin-success]');
                const errorAlert = root.querySelector('[data-doc-admin-error]');
                const title = downloadLink.getAttribute('data-document-title') || 'document';
                setAlert(root, errorAlert, '', { autoHide: false });
                setAlert(root, successAlert, `Download started for ${title}.`);
            }
        });

        root.addEventListener('submit', function (event) {
            const form = event.target.closest('[data-doc-admin-form]');
            if (!form) {
                return;
            }

            event.preventDefault();
            uploadDocument(root, form);
        });

        root.querySelector('[data-doc-admin-search]')?.addEventListener('input', function () {
            applySearchFilter(root);
        });

        applySearchFilter(root);
    };
})();
