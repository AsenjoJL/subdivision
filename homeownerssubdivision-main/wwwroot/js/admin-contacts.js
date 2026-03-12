(function () {
    function getAlertTimers(root) {
        if (!root.__adminContactAlertTimers) {
            root.__adminContactAlertTimers = new Map();
        }

        return root.__adminContactAlertTimers;
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

        element.classList.toggle('admin-contacts-hidden', !message);

        const settings = options || {};
        if (message && settings.autoHide !== false) {
            const timers = getAlertTimers(root);
            const timerId = window.setTimeout(function () {
                element.classList.add('admin-contacts-hidden');
                timers.delete(element);
            }, settings.duration || 3200);
            timers.set(element, timerId);
        }
    }

    function togglePanel(panel, show) {
        if (!panel) {
            return;
        }

        const shouldShow = typeof show === 'boolean'
            ? show
            : panel.hasAttribute('hidden');

        panel.hidden = !shouldShow;
        panel.classList.toggle('admin-contacts-hidden', !shouldShow);
        document.body.classList.toggle('admin-contacts-modal-open', shouldShow);

        if (shouldShow) {
            panel.querySelector('input, select, textarea')?.focus();
        }
    }

    function getFields(root) {
        const fields = {};
        root.querySelectorAll('[data-contact-admin-field]').forEach((field) => {
            fields[field.getAttribute('data-contact-admin-field')] = field;
        });
        return fields;
    }

    function getContactValue(contact, key) {
        if (!contact) {
            return undefined;
        }

        if (Object.prototype.hasOwnProperty.call(contact, key)) {
            return contact[key];
        }

        const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
        if (Object.prototype.hasOwnProperty.call(contact, camelKey)) {
            return contact[camelKey];
        }

        return undefined;
    }

    function buildContactFormData(form) {
        const formData = new FormData(form);
        const checkboxFields = ['IsEmergency', 'IsActive'];

        checkboxFields.forEach((fieldName) => {
            const field = form.querySelector(`[data-contact-admin-field="${fieldName}"]`);
            formData.delete(fieldName);
            formData.append(fieldName, field?.checked ? 'true' : 'false');
        });

        return formData;
    }

    function resetForm(root) {
        const form = root.querySelector('[data-contact-admin-form]');
        form?.reset();

        const fields = getFields(root);
        if (fields.ContactID) {
            fields.ContactID.value = '0';
        }
        if (fields.DisplayOrder) {
            fields.DisplayOrder.value = '0';
        }
        if (fields.IsActive) {
            fields.IsActive.checked = true;
        }

        root.dataset.mode = 'create';
        const modeLabel = root.querySelector('[data-contact-admin-mode-label]');
        const titleNode = root.querySelector('[data-contact-admin-title]');
        const submitLabel = root.querySelector('[data-contact-admin-submit] span');
        if (modeLabel) {
            modeLabel.textContent = 'Create';
        }
        if (titleNode) {
            titleNode.textContent = 'Add contact';
        }
        if (submitLabel) {
            submitLabel.textContent = 'Save contact';
        }
    }

    function fillForm(root, contact) {
        const fields = getFields(root);
        Object.entries(fields).forEach(([key, field]) => {
            const value = getContactValue(contact, key);
            if (field.type === 'checkbox') {
                field.checked = Boolean(value);
            } else {
                field.value = value ?? (field.name === 'DisplayOrder' ? '0' : '');
            }
        });

        root.dataset.mode = 'edit';
        const modeLabel = root.querySelector('[data-contact-admin-mode-label]');
        const titleNode = root.querySelector('[data-contact-admin-title]');
        const submitLabel = root.querySelector('[data-contact-admin-submit] span');
        if (modeLabel) {
            modeLabel.textContent = 'Edit';
        }
        if (titleNode) {
            titleNode.textContent = 'Edit contact';
        }
        if (submitLabel) {
            submitLabel.textContent = 'Update contact';
        }
    }

    function updateVisibleCount(root) {
        const visibleCount = root.querySelectorAll('[data-contact-admin-card]:not(.admin-contacts-hidden)').length;
        root.querySelectorAll('[data-contact-admin-visible-count]').forEach((node) => {
            node.textContent = visibleCount.toString();
        });
    }

    function applySearchFilter(root) {
        const query = (root.querySelector('[data-contact-admin-search]')?.value || '').trim().toLowerCase();
        root.querySelectorAll('[data-contact-admin-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const matches = !query || searchValue.includes(query);
            card.classList.toggle('admin-contacts-hidden', !matches);
        });

        const emptyState = root.querySelector('[data-contact-admin-empty]');
        const anyVisible = root.querySelector('[data-contact-admin-card]:not(.admin-contacts-hidden)');
        emptyState?.classList.toggle('admin-contacts-hidden', Boolean(anyVisible));
        updateVisibleCount(root);
    }

    async function refreshContactCards(root) {
        const listHost = root.querySelector('[data-contact-admin-list-host]');
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
                throw new Error('Failed to refresh contacts.');
            }

            listHost.innerHTML = await response.text();
            applySearchFilter(root);
        } finally {
            listHost.classList.remove('is-loading');
        }
    }

    async function saveContact(root, form) {
        const successAlert = root.querySelector('[data-contact-admin-success]');
        const errorAlert = root.querySelector('[data-contact-admin-error]');
        const submitButton = root.querySelector('[data-contact-admin-submit]');

        setAlert(root, successAlert, '', { autoHide: false });
        setAlert(root, errorAlert, '', { autoHide: false });

        if (!form.reportValidity()) {
            return;
        }

        const mode = root.dataset.mode === 'edit' ? 'Edit' : 'Add';
        const panel = root.querySelector('[data-contact-admin-panel]');
        const actionUrl = mode === 'Edit'
            ? panel?.getAttribute('data-edit-url')
            : panel?.getAttribute('data-add-url');

        if (submitButton) {
            submitButton.disabled = true;
        }

        try {
            const response = await fetch(actionUrl || `/Contact/${mode}`, {
                method: 'POST',
                body: buildContactFormData(form)
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                const validationMessage = result.errors
                    ? Object.values(result.errors).flat().find(Boolean)
                    : '';
                setAlert(root, errorAlert, validationMessage || result.message || 'Unable to save contact.');
                return;
            }

            await refreshContactCards(root);
            resetForm(root);
            togglePanel(root.querySelector('[data-contact-admin-panel]'), false);
            setAlert(root, successAlert, result.message || 'Contact saved successfully.');
        } catch (error) {
            setAlert(root, errorAlert, 'Unable to save contact right now. Please try again.');
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    async function loadContactForEdit(root, contactId) {
        const successAlert = root.querySelector('[data-contact-admin-success]');
        const errorAlert = root.querySelector('[data-contact-admin-error]');
        setAlert(root, successAlert, '', { autoHide: false });
        setAlert(root, errorAlert, '', { autoHide: false });

        try {
            const panel = root.querySelector('[data-contact-admin-panel]');
            const getUrl = panel?.getAttribute('data-get-url') || '/Contact/GetContact';
            const separator = getUrl.includes('?') ? '&' : '?';
            const response = await fetch(`${getUrl}${separator}id=${encodeURIComponent(contactId)}`, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                }
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, errorAlert, result.message || 'Unable to load this contact.');
                return;
            }

            fillForm(root, result.contact);
            togglePanel(root.querySelector('[data-contact-admin-panel]'), true);
        } catch (error) {
            setAlert(root, errorAlert, 'Unable to load this contact right now.');
        }
    }

    async function deleteContact(root, button) {
        const successAlert = root.querySelector('[data-contact-admin-success]');
        const errorAlert = root.querySelector('[data-contact-admin-error]');
        const contactId = button.getAttribute('data-delete-id');
        const deleteUrl = button.getAttribute('data-delete-url');
        if (!contactId || !deleteUrl) {
            return;
        }

        if (!window.confirm('Delete this contact?')) {
            return;
        }

        button.disabled = true;
        setAlert(root, successAlert, '', { autoHide: false });
        setAlert(root, errorAlert, '', { autoHide: false });

        try {
            const formData = new FormData();
            formData.append('id', contactId);

            const response = await fetch(deleteUrl, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, errorAlert, result.message || 'Unable to delete contact.');
                return;
            }

            await refreshContactCards(root);
            setAlert(root, successAlert, result.message || 'Contact deleted successfully.');
        } catch (error) {
            setAlert(root, errorAlert, 'Unable to delete contact right now. Please try again.');
        } finally {
            button.disabled = false;
        }
    }

    window.initializeAdminContactsModule = function initializeAdminContactsModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';
        root.dataset.mode = 'create';
        root.__adminContactsEscHandler = function (event) {
            if (event.key !== 'Escape') {
                return;
            }

            const panel = root.querySelector('[data-contact-admin-panel]');
            if (panel && !panel.hasAttribute('hidden')) {
                resetForm(root);
                togglePanel(panel, false);
            }
        };
        document.addEventListener('keydown', root.__adminContactsEscHandler);

        root.addEventListener('click', function (event) {
            const toggleButton = event.target.closest('[data-contact-admin-toggle]');
            if (toggleButton) {
                resetForm(root);
                togglePanel(root.querySelector('[data-contact-admin-panel]'));
                return;
            }

            const cancelButton = event.target.closest('[data-contact-admin-cancel]');
            if (cancelButton) {
                resetForm(root);
                togglePanel(root.querySelector('[data-contact-admin-panel]'), false);
                return;
            }

            const editButton = event.target.closest('[data-contact-admin-edit]');
            if (editButton) {
                loadContactForEdit(root, editButton.getAttribute('data-edit-id'));
                return;
            }

            const deleteButton = event.target.closest('[data-contact-admin-delete]');
            if (deleteButton) {
                deleteContact(root, deleteButton);
            }
        });

        root.addEventListener('submit', function (event) {
            const form = event.target.closest('[data-contact-admin-form]');
            if (!form) {
                return;
            }

            event.preventDefault();
            saveContact(root, form);
        });

        root.querySelector('[data-contact-admin-search]')?.addEventListener('input', function () {
            applySearchFilter(root);
        });

        applySearchFilter(root);

        if (typeof window.__registerAdminEmbeddedCleanup === 'function') {
            window.__registerAdminEmbeddedCleanup(function () {
                document.removeEventListener('keydown', root.__adminContactsEscHandler);
                document.body.classList.remove('admin-contacts-modal-open');
            });
        }
    };
})();
