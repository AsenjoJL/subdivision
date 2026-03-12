(function () {
    function setAlert(root, selector, message, autoHide) {
        const alert = root.querySelector(selector);
        if (!alert) {
            return;
        }

        const text = alert.querySelector('span');
        if (text) {
            text.textContent = message || '';
        }

        window.clearTimeout(alert.__hideTimer);

        if (!message) {
            alert.classList.add('admin-announcements-hidden');
            return;
        }

        alert.classList.remove('admin-announcements-hidden');
        if (autoHide !== false) {
            alert.__hideTimer = window.setTimeout(function () {
                alert.classList.add('admin-announcements-hidden');
            }, 3200);
        }
    }

    function clearFieldErrors(root) {
        root.querySelectorAll('[data-announcement-field-error]').forEach((node) => {
            node.textContent = '';
        });
        root.querySelectorAll('[data-announcement-field-input]').forEach((node) => {
            node.classList.remove('is-invalid');
        });
    }

    function applyValidationErrors(root, validationErrors) {
        clearFieldErrors(root);
        Object.entries(validationErrors || {}).forEach(([key, messages]) => {
            const input = root.querySelector(`[data-announcement-field-input="${key}"]`);
            const error = root.querySelector(`[data-announcement-field-error="${key}"]`);
            if (input) {
                input.classList.add('is-invalid');
            }
            if (error) {
                error.textContent = Array.isArray(messages) ? messages.join(' ') : (messages || '');
            }
        });
    }

    async function parseJsonResponse(response) {
        const contentType = response.headers.get('content-type') || '';
        if (!contentType.includes('application/json')) {
            throw new Error(await response.text() || 'Unexpected server response.');
        }

        return response.json();
    }

    function openModal(root, mode, announcement) {
        const modal = root.querySelector('[data-announcement-modal]');
        const form = root.querySelector('[data-announcement-form]');
        if (!modal || !form) {
            return;
        }

        form.reset();
        clearFieldErrors(root);
        setAlert(root, '[data-announcement-modal-success]', '');
        setAlert(root, '[data-announcement-modal-error]', '');

        form.dataset.mode = mode;
        root.querySelector('[data-announcement-modal-title]').textContent = mode === 'edit' ? 'Edit announcement' : 'Post announcement';
        root.querySelector('[data-announcement-modal-kicker]').textContent = mode === 'edit' ? 'Update' : 'Create';

        const value = announcement || { AnnouncementID: 0, title: '', content: '', isUrgent: false };
        root.querySelector('[data-announcement-field-input="AnnouncementID"]').value = value.AnnouncementID || 0;
        root.querySelector('[data-announcement-field-input="Title"]').value = value.title || '';
        root.querySelector('[data-announcement-field-input="Content"]').value = value.content || '';
        root.querySelector('[data-announcement-field-input="IsUrgent"]').checked = Boolean(value.isUrgent);

        modal.classList.remove('admin-announcements-hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('admin-announcements-modal-open');
    }

    function closeModal(root) {
        const modal = root.querySelector('[data-announcement-modal]');
        if (!modal) {
            return;
        }

        modal.classList.add('admin-announcements-hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('admin-announcements-modal-open');
        clearFieldErrors(root);
        setAlert(root, '[data-announcement-modal-success]', '');
        setAlert(root, '[data-announcement-modal-error]', '');
    }

    function runClientValidation(root) {
        clearFieldErrors(root);
        const validationErrors = {};
        root.querySelectorAll('[data-announcement-field-input]').forEach((input) => {
            if (!(input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement)) {
                return;
            }
            if (input.type !== 'checkbox' && !input.checkValidity()) {
                const key = input.getAttribute('data-announcement-field-input');
                if (key) {
                    validationErrors[key] = [input.validationMessage || 'This field is required.'];
                }
            }
        });

        if (Object.keys(validationErrors).length > 0) {
            applyValidationErrors(root, validationErrors);
            return false;
        }

        return true;
    }

    function updateStats(root) {
        const cards = Array.from(root.querySelectorAll('[data-announcement-card]'));
        const visibleCards = cards.filter((card) => !card.classList.contains('admin-announcements-hidden'));
        const urgentCount = visibleCards.filter((card) => card.getAttribute('data-announcement-urgent') === 'true').length;

        root.querySelector('[data-announcement-total]').textContent = String(cards.length);
        root.querySelector('[data-announcement-urgent]').textContent = String(urgentCount);
        root.querySelector('[data-announcement-week]').textContent = String(visibleCards.length);
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-announcement-search]')?.value || '').trim().toLowerCase();
        const filter = root.querySelector('[data-announcement-filter]')?.value || 'All';
        let visible = 0;

        root.querySelectorAll('[data-announcement-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const isUrgent = card.getAttribute('data-announcement-urgent') === 'true';
            const matchesSearch = !query || searchValue.includes(query);
            const matchesFilter = filter === 'All' || (filter === 'Urgent' ? isUrgent : !isUrgent);
            const shouldShow = matchesSearch && matchesFilter;
            card.classList.toggle('admin-announcements-hidden', !shouldShow);
            if (shouldShow) {
                visible += 1;
            }
        });

        root.querySelector('[data-announcement-empty]')?.classList.toggle('admin-announcements-hidden', visible > 0);
        updateStats(root);
    }

    async function refreshList(root) {
        const host = root.querySelector('[data-announcement-list-host]');
        const loadUrl = host?.getAttribute('data-load-url');
        if (!host || !loadUrl) {
            return;
        }

        host.classList.add('is-loading');
        try {
            const response = await fetch(loadUrl, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) {
                throw new Error('Failed to refresh announcements.');
            }
            host.innerHTML = await response.text();
            applyFilters(root);
        } finally {
            host.classList.remove('is-loading');
        }
    }

    async function submitForm(root) {
        const form = root.querySelector('[data-announcement-form]');
        const submitButton = root.querySelector('[data-announcement-submit]');
        const submitLabel = root.querySelector('[data-announcement-submit-label]');
        const actionUrl = form.dataset.mode === 'edit'
            ? form.getAttribute('data-edit-url')
            : form.getAttribute('data-create-url');

        if (!form || !actionUrl) {
            return;
        }

        setAlert(root, '[data-announcement-success]', '');
        setAlert(root, '[data-announcement-error]', '');

        if (!runClientValidation(root)) {
            setAlert(root, '[data-announcement-modal-error]', 'Please complete the required announcement details.', false);
            return;
        }

        submitButton.disabled = true;
        submitLabel.textContent = 'Saving...';

        try {
            const formData = new FormData(form);
            if (!root.querySelector('[data-announcement-field-input="IsUrgent"]').checked) {
                formData.set('IsUrgent', 'false');
            }

            const response = await fetch(actionUrl, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            });
            const result = await parseJsonResponse(response);
            if (!response.ok || !result.success) {
                applyValidationErrors(root, result.validationErrors);
                setAlert(root, '[data-announcement-modal-error]', result.message || 'Unable to save announcement.', false);
                setAlert(root, '[data-announcement-error]', result.message || 'Unable to save announcement.');
                return;
            }

            await refreshList(root);
            closeModal(root);
            setAlert(root, '[data-announcement-success]', result.message || 'Announcement saved successfully!');
        } catch (error) {
            setAlert(root, '[data-announcement-modal-error]', 'Unable to save announcement right now. Please try again.', false);
            setAlert(root, '[data-announcement-error]', 'Unable to save announcement right now. Please try again.');
        } finally {
            submitButton.disabled = false;
            submitLabel.textContent = 'Save announcement';
        }
    }

    async function loadAnnouncement(root, id) {
        try {
            const response = await fetch(`/Admin/GetAnnouncementDetails?id=${encodeURIComponent(id)}`, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                }
            });
            const result = await parseJsonResponse(response);
            if (!response.ok || !result.success) {
                setAlert(root, '[data-announcement-error]', result.message || 'Unable to load announcement.');
                return;
            }

            openModal(root, 'edit', result.announcement);
        } catch (error) {
            setAlert(root, '[data-announcement-error]', 'Unable to load announcement right now. Please try again.');
        }
    }

    async function deleteAnnouncement(root, id) {
        if (!window.confirm('Delete this announcement?')) {
            return;
        }

        try {
            const formData = new FormData();
            formData.append('id', id);
            const response = await fetch('/Admin/DeleteAnnouncement', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            });
            const result = await parseJsonResponse(response);
            if (!response.ok || !result.success) {
                setAlert(root, '[data-announcement-error]', result.message || 'Unable to delete announcement.');
                return;
            }

            await refreshList(root);
            setAlert(root, '[data-announcement-success]', result.message || 'Announcement deleted successfully!');
        } catch (error) {
            setAlert(root, '[data-announcement-error]', 'Unable to delete announcement right now. Please try again.');
        }
    }

    window.initializeAdminAnnouncementsModule = function initializeAdminAnnouncementsModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';
        const handleKeydown = function (event) {
            if (event.key === 'Escape') {
                closeModal(root);
            }
        };

        root.addEventListener('click', function (event) {
            if (event.target.closest('[data-announcement-open-create]')) {
                openModal(root, 'create');
                return;
            }
            if (event.target.closest('[data-announcement-close]')) {
                closeModal(root);
                return;
            }

            const editButton = event.target.closest('[data-announcement-edit]');
            if (editButton) {
                loadAnnouncement(root, editButton.getAttribute('data-announcement-edit'));
                return;
            }

            const deleteButton = event.target.closest('[data-announcement-delete]');
            if (deleteButton) {
                deleteAnnouncement(root, deleteButton.getAttribute('data-announcement-delete'));
            }
        });

        root.addEventListener('submit', function (event) {
            const form = event.target.closest('[data-announcement-form]');
            if (!form) {
                return;
            }

            event.preventDefault();
            submitForm(root);
        });

        root.querySelector('[data-announcement-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });
        root.querySelector('[data-announcement-filter]')?.addEventListener('change', function () {
            applyFilters(root);
        });

        document.addEventListener('keydown', handleKeydown);
        if (typeof window.__registerAdminEmbeddedCleanup === 'function') {
            window.__registerAdminEmbeddedCleanup(function () {
                document.removeEventListener('keydown', handleKeydown);
            });
        }

        applyFilters(root);
    };
})();
