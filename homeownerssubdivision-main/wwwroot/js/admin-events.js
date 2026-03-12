(function () {
    function setAlert(root, type, message) {
        const successAlert = root.querySelector('[data-event-success]');
        const errorAlert = root.querySelector('[data-event-error]');
        const target = type === 'success' ? successAlert : errorAlert;
        const other = type === 'success' ? errorAlert : successAlert;

        if (other) {
            other.classList.add('admin-events-hidden');
        }

        if (!target) {
            return;
        }

        const text = target.querySelector('span');
        if (text) {
            text.textContent = message || '';
        }

        if (!message) {
            target.classList.add('admin-events-hidden');
            return;
        }

        target.classList.remove('admin-events-hidden');
        window.clearTimeout(target.__hideTimer);
        target.__hideTimer = window.setTimeout(function () {
            target.classList.add('admin-events-hidden');
        }, 3200);
    }

    function setModalAlert(root, type, message, options) {
        const successAlert = root.querySelector('[data-event-modal-success]');
        const errorAlert = root.querySelector('[data-event-modal-error]');
        const target = type === 'success' ? successAlert : errorAlert;
        const other = type === 'success' ? errorAlert : successAlert;

        if (other) {
            other.classList.add('admin-events-hidden');
            window.clearTimeout(other.__hideTimer);
        }

        if (!target) {
            return;
        }

        const text = target.querySelector('span');
        if (text) {
            text.textContent = message || '';
        }

        if (!message) {
            target.classList.add('admin-events-hidden');
            window.clearTimeout(target.__hideTimer);
            return;
        }

        target.classList.remove('admin-events-hidden');
        const settings = options || {};
        window.clearTimeout(target.__hideTimer);
        if (settings.autoHide !== false) {
            target.__hideTimer = window.setTimeout(function () {
                target.classList.add('admin-events-hidden');
            }, settings.duration || 3200);
        }
    }

    function clearFieldErrors(root) {
        root.querySelectorAll('[data-event-field-error]').forEach((node) => {
            node.textContent = '';
        });
        root.querySelectorAll('[data-event-field-input]').forEach((node) => {
            node.classList.remove('is-invalid');
        });
    }

    async function parseJsonResponse(response) {
        const contentType = response.headers.get('content-type') || '';
        if (!contentType.includes('application/json')) {
            const text = await response.text();
            throw new Error(text || 'The server returned an unexpected response.');
        }

        return response.json();
    }

    function runClientValidation(root) {
        clearFieldErrors(root);
        const validationErrors = {};

        root.querySelectorAll('[data-event-field-input]').forEach((input) => {
            if (!(input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement || input instanceof HTMLSelectElement)) {
                return;
            }

            if (!input.checkValidity()) {
                const key = input.getAttribute('data-event-field-input');
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

    function applyValidationErrors(root, validationErrors) {
        clearFieldErrors(root);
        Object.entries(validationErrors || {}).forEach(([key, messages]) => {
            const input = root.querySelector(`[data-event-field-input="${key}"]`);
            const error = root.querySelector(`[data-event-field-error="${key}"]`);
            if (input) {
                input.classList.add('is-invalid');
            }
            if (error) {
                error.textContent = Array.isArray(messages) ? messages.join(' ') : (messages || '');
            }
        });
    }

    function openModal(root, mode, eventItem) {
        const modal = root.querySelector('[data-event-modal]');
        const title = root.querySelector('[data-event-modal-title]');
        const kicker = root.querySelector('[data-event-modal-kicker]');
        const form = root.querySelector('[data-event-form]');

        if (!modal || !form) {
            return;
        }

        clearFieldErrors(root);
        form.reset();
        setModalAlert(root, 'success', '');
        setModalAlert(root, 'error', '');

        form.dataset.mode = mode;
        if (title) {
            title.textContent = mode === 'edit' ? 'Edit event' : 'Add event';
        }
        if (kicker) {
            kicker.textContent = mode === 'edit' ? 'Update' : 'Create';
        }

        const values = eventItem || {
            EventID: 0,
            title: '',
            description: '',
            eventDate: '',
            category: '',
            location: ''
        };

        root.querySelector('[data-event-field-input="EventID"]').value = values.EventID || values.eventID || 0;
        root.querySelector('[data-event-field-input="Title"]').value = values.title || '';
        root.querySelector('[data-event-field-input="Description"]').value = values.description || '';
        root.querySelector('[data-event-field-input="EventDate"]').value = values.eventDate || '';
        root.querySelector('[data-event-field-input="Category"]').value = values.category || root.querySelector('[data-event-field-input="Category"] option')?.value || '';
        root.querySelector('[data-event-field-input="Location"]').value = values.location || '';

        modal.classList.remove('admin-events-hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('admin-events-modal-open');
        root.querySelector('[data-event-field-input="Title"]')?.focus();
    }

    function closeModal(root) {
        const modal = root.querySelector('[data-event-modal]');
        if (!modal) {
            return;
        }

        modal.classList.add('admin-events-hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('admin-events-modal-open');
        clearFieldErrors(root);
        setModalAlert(root, 'success', '');
        setModalAlert(root, 'error', '');
    }

    function updateStats(root) {
        const cards = Array.from(root.querySelectorAll('[data-event-card]:not(.admin-events-hidden)'));
        const total = root.querySelectorAll('[data-event-card]').length;
        const today = new Date();
        today.setHours(0, 0, 0, 0);

        let upcoming = 0;
        let thisMonth = 0;

        cards.forEach((card) => {
            const value = card.getAttribute('data-event-date');
            if (!value) {
                return;
            }

            const eventDate = new Date(value);
            if (eventDate >= today) {
                upcoming += 1;
            }
            if (eventDate.getFullYear() === today.getFullYear() && eventDate.getMonth() === today.getMonth()) {
                thisMonth += 1;
            }
        });

        const totalNode = root.querySelector('[data-event-total]');
        const upcomingNode = root.querySelector('[data-event-upcoming]');
        const monthNode = root.querySelector('[data-event-month]');

        if (totalNode) {
            totalNode.textContent = total.toString();
        }
        if (upcomingNode) {
            upcomingNode.textContent = upcoming.toString();
        }
        if (monthNode) {
            monthNode.textContent = thisMonth.toString();
        }
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-event-search]')?.value || '').trim().toLowerCase();
        const category = root.querySelector('[data-event-category-filter]')?.value || 'All';
        let visible = 0;

        root.querySelectorAll('[data-event-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const categoryValue = card.getAttribute('data-event-category') || '';
            const matchesSearch = !query || searchValue.includes(query);
            const matchesCategory = category === 'All' || categoryValue === category;
            const shouldShow = matchesSearch && matchesCategory;
            card.classList.toggle('admin-events-hidden', !shouldShow);
            if (shouldShow) {
                visible += 1;
            }
        });

        const emptyState = root.querySelector('[data-event-empty]');
        emptyState?.classList.toggle('admin-events-hidden', visible > 0);
        updateStats(root);
    }

    async function refreshList(root) {
        const host = root.querySelector('[data-event-list-host]');
        const loadUrl = host?.getAttribute('data-load-url');
        if (!host || !loadUrl) {
            return;
        }

        host.classList.add('is-loading');
        try {
            const response = await fetch(loadUrl, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error('Failed to refresh events.');
            }

            host.innerHTML = await response.text();
            applyFilters(root);
        } finally {
            host.classList.remove('is-loading');
        }
    }

    async function submitForm(root) {
        const form = root.querySelector('[data-event-form]');
        if (!form) {
            return;
        }

        const submitButton = root.querySelector('[data-event-submit]');
        const submitLabel = root.querySelector('[data-event-submit-label]');
        const actionUrl = form.dataset.mode === 'edit'
            ? form.getAttribute('data-edit-url')
            : form.getAttribute('data-create-url');

        if (!actionUrl) {
            return;
        }

        submitButton && (submitButton.disabled = true);
        if (submitLabel) {
            submitLabel.textContent = 'Saving...';
        }
        setAlert(root, 'success', '');
        setAlert(root, 'error', '');
        setModalAlert(root, 'success', '');
        setModalAlert(root, 'error', '');
        clearFieldErrors(root);

        if (!runClientValidation(root)) {
            submitButton && (submitButton.disabled = false);
            if (submitLabel) {
                submitLabel.textContent = 'Save event';
            }
            setModalAlert(root, 'error', 'Please complete the required event details.', { autoHide: false });
            setAlert(root, 'error', 'Please complete the required event details.');
            return;
        }

        try {
            const response = await fetch(actionUrl, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: new FormData(form)
            });

            const result = await parseJsonResponse(response);
            if (!response.ok || !result.success) {
                applyValidationErrors(root, result.validationErrors);
                setModalAlert(root, 'error', result.message || 'Unable to save event.', { autoHide: false });
                setAlert(root, 'error', result.message || 'Unable to save event.');
                return;
            }

            await refreshList(root);
            setModalAlert(root, 'success', result.message || 'Event saved successfully.');
            closeModal(root);
            setAlert(root, 'success', result.message || 'Event saved successfully!');
        } catch (error) {
            setModalAlert(root, 'error', 'Unable to save event right now. Please try again.', { autoHide: false });
            setAlert(root, 'error', 'Unable to save event right now. Please try again.');
        } finally {
            submitButton && (submitButton.disabled = false);
            if (submitLabel) {
                submitLabel.textContent = 'Save event';
            }
        }
    }

    async function loadEventForEdit(root, eventId) {
        const detailsUrl = `/Admin/GetEventDetails?id=${encodeURIComponent(eventId)}`;
        setAlert(root, 'success', '');
        setAlert(root, 'error', '');

        try {
            const response = await fetch(detailsUrl, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                }
            });
            const result = await parseJsonResponse(response);
            if (!response.ok || !result.success) {
                setAlert(root, 'error', result.message || 'Unable to load event.');
                return;
            }

            openModal(root, 'edit', result.eventItem);
        } catch (error) {
            setAlert(root, 'error', 'Unable to load event right now. Please try again.');
        }
    }

    async function deleteEvent(root, eventId) {
        if (!window.confirm('Delete this event?')) {
            return;
        }

        setAlert(root, 'success', '');
        setAlert(root, 'error', '');

        try {
            const formData = new FormData();
            formData.append('id', eventId);

            const response = await fetch('/Admin/DeleteEvent', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData
            });

            const result = await parseJsonResponse(response);
            if (!response.ok || !result.success) {
                setAlert(root, 'error', result.message || 'Unable to delete event.');
                return;
            }

            await refreshList(root);
            setAlert(root, 'success', result.message || 'Event deleted successfully!');
        } catch (error) {
            setAlert(root, 'error', 'Unable to delete event right now. Please try again.');
        }
    }

    window.initializeAdminEventsModule = function initializeAdminEventsModule(root) {
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
            if (event.target.closest('[data-event-open-create]')) {
                openModal(root, 'create');
                return;
            }

            if (event.target.closest('[data-event-close]')) {
                closeModal(root);
                return;
            }

            const editButton = event.target.closest('[data-event-edit]');
            if (editButton) {
                loadEventForEdit(root, editButton.getAttribute('data-event-edit'));
                return;
            }

            const deleteButton = event.target.closest('[data-event-delete]');
            if (deleteButton) {
                deleteEvent(root, deleteButton.getAttribute('data-event-delete'));
            }
        });

        root.addEventListener('submit', function (event) {
            const form = event.target.closest('[data-event-form]');
            if (!form) {
                return;
            }

            event.preventDefault();
            submitForm(root);
        });

        root.querySelector('[data-event-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        root.querySelector('[data-event-category-filter]')?.addEventListener('change', function () {
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
