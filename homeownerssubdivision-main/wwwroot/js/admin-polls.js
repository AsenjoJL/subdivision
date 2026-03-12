(function () {
    function setButtonLoading(button, isLoading, loadingLabel, idleLabel) {
        if (!button) {
            return;
        }

        const icon = button.querySelector('i');
        const text = button.querySelector('span');

        button.disabled = isLoading;
        button.classList.toggle('is-loading', isLoading);
        button.setAttribute('aria-busy', isLoading ? 'true' : 'false');

        if (icon) {
            icon.className = isLoading ? 'fas fa-spinner' : 'fas fa-floppy-disk';
        }

        if (text) {
            text.textContent = isLoading ? loadingLabel : idleLabel;
        }
    }

    function setAlert(root, type, message, inModal) {
        const prefix = inModal ? '[data-admin-polls-modal-' : '[data-admin-polls-';
        const success = root.querySelector(`${prefix}success]`);
        const error = root.querySelector(`${prefix}error]`);
        const target = type === 'success' ? success : error;
        const other = type === 'success' ? error : success;

        if (other) {
            other.classList.add('admin-polls-hidden');
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
            target.classList.add('admin-polls-hidden');
            window.clearTimeout(target.__hideTimer);
            return;
        }

        target.classList.remove('admin-polls-hidden');
        window.clearTimeout(target.__hideTimer);
        target.__hideTimer = window.setTimeout(function () {
            target.classList.add('admin-polls-hidden');
        }, 3200);
    }

    function clearFieldErrors(root) {
        root.querySelectorAll('[data-admin-polls-field-error]').forEach((node) => {
            node.textContent = '';
        });
    }

    function applyFieldErrors(root, errors) {
        clearFieldErrors(root);
        Object.entries(errors || {}).forEach(([key, value]) => {
            const normalizedKey = key.replace(/^model\./i, '');
            const field = root.querySelector(`[data-admin-polls-field-error="${normalizedKey}"]`);
            if (field) {
                field.textContent = Array.isArray(value) ? value.join(' ') : (value || '');
            }
        });
    }

    function openCreateModal(root) {
        const modal = root.querySelector('[data-admin-polls-modal]');
        if (!modal) {
            return;
        }

        modal.classList.remove('admin-polls-hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('admin-polls-modal-open');
        setAlert(root, 'success', '', true);
        setAlert(root, 'error', '', true);
        clearFieldErrors(root);

        const form = root.querySelector('[data-admin-polls-form]');
        if (form) {
            form.reset();
        }

        const optionsHost = root.querySelector('[data-admin-polls-options]');
        if (optionsHost) {
            optionsHost.innerHTML = '';
            addOptionField(root);
            addOptionField(root);
        }

        root.querySelector('[data-admin-polls-field="Question"]')?.focus();
    }

    function closeCreateModal(root) {
        const modal = root.querySelector('[data-admin-polls-modal]');
        if (!modal) {
            return;
        }

        modal.classList.add('admin-polls-hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('admin-polls-modal-open');
    }

    function openResultsModal(root, poll) {
        const modal = root.querySelector('[data-admin-polls-results-modal]');
        const title = root.querySelector('[data-admin-polls-results-title]');
        const body = root.querySelector('[data-admin-polls-results-body]');
        if (!modal || !title || !body) {
            return;
        }

        title.textContent = poll.question || 'Poll results';
        const totalVotes = poll.totalVotes || 0;
        body.innerHTML = `
            <p class="admin-polls-results-summary">${poll.description || 'No description provided.'}</p>
            <div class="admin-polls-results-list">
                ${(poll.options || []).map(function (option) {
                    const percentage = totalVotes > 0 ? ((option.voteCount || 0) * 100 / totalVotes).toFixed(1) : '0.0';
                    return `
                        <article class="admin-polls-result-item">
                            <div class="admin-polls-result-row">
                                <strong>${option.optionText}</strong>
                                <span>${option.voteCount || 0} votes · ${percentage}%</span>
                            </div>
                            <div class="admin-polls-result-bar">
                                <span style="width:${percentage}%"></span>
                            </div>
                        </article>
                    `;
                }).join('')}
            </div>
        `;

        modal.classList.remove('admin-polls-hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('admin-polls-modal-open');
    }

    function closeResultsModal(root) {
        const modal = root.querySelector('[data-admin-polls-results-modal]');
        if (!modal) {
            return;
        }

        modal.classList.add('admin-polls-hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('admin-polls-modal-open');
    }

    function addOptionField(root, value) {
        const host = root.querySelector('[data-admin-polls-options]');
        if (!host) {
            return;
        }

        const row = document.createElement('div');
        row.className = 'admin-polls-option-row';
        row.innerHTML = `
            <input type="text" maxlength="200" value="${value || ''}" placeholder="Option text" data-admin-polls-option-input />
            <button type="button" class="admin-polls-option-remove" data-admin-polls-remove-option aria-label="Remove option">
                <i class="fas fa-times"></i>
            </button>
        `;
        host.appendChild(row);
    }

    function getPayload(root) {
        const form = root.querySelector('[data-admin-polls-form]');
        const options = Array.from(root.querySelectorAll('[data-admin-polls-option-input]'))
            .map((input) => input.value.trim())
            .filter(Boolean);

        return {
            Question: form.querySelector('[data-admin-polls-field="Question"]').value.trim(),
            Description: form.querySelector('[data-admin-polls-field="Description"]').value.trim(),
            StartDate: form.querySelector('[data-admin-polls-field="StartDate"]').value || null,
            EndDate: form.querySelector('[data-admin-polls-field="EndDate"]').value || null,
            Status: form.querySelector('[data-admin-polls-field="Status"]').value,
            IsAnonymous: form.querySelector('[data-admin-polls-field="IsAnonymous"]').checked,
            AllowMultipleChoices: form.querySelector('[data-admin-polls-field="AllowMultipleChoices"]').checked,
            Options: options
        };
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-admin-polls-search]')?.value || '').trim().toLowerCase();
        const status = root.querySelector('[data-admin-polls-filter]')?.value || 'All';
        let visible = 0;

        root.querySelectorAll('[data-admin-poll-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const cardStatus = card.getAttribute('data-poll-status') || '';
            const matches = (!query || searchValue.includes(query)) && (status === 'All' || status === cardStatus);
            card.classList.toggle('admin-polls-hidden', !matches);
            if (matches) {
                visible += 1;
            }
        });

        const empty = root.querySelector('[data-admin-polls-empty]');
        if (empty && root.querySelectorAll('[data-admin-poll-card]').length > 0) {
            empty.classList.toggle('admin-polls-hidden', visible > 0);
        }

        const total = root.querySelector('[data-admin-polls-total]');
        if (total) {
            total.textContent = visible.toString();
        }
    }

    async function refreshList(root) {
        const host = root.querySelector('[data-admin-polls-list-host]');
        const loadUrl = root.getAttribute('data-load-url');
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
                throw new Error('Unable to refresh polls.');
            }

            host.innerHTML = await response.text();
            applyFilters(root);
        } finally {
            host.classList.remove('is-loading');
        }
    }

    async function createPoll(root) {
        const createUrl = root.getAttribute('data-create-url');
        const submit = root.querySelector('[data-admin-polls-submit]');
        if (!createUrl || !submit) {
            return;
        }

        setAlert(root, 'error', '', true);
        clearFieldErrors(root);
        setButtonLoading(submit, true, 'Saving...', 'Save poll');

        try {
            const response = await fetch(createUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(getPayload(root))
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                if (result.errors) {
                    applyFieldErrors(root, result.errors);
                }
                setAlert(root, 'error', result.message || 'Unable to save this poll.', true);
                return;
            }

            closeCreateModal(root);
            setAlert(root, 'success', result.message || 'Poll created successfully!', false);
            refreshList(root).catch(function () {
                setAlert(root, 'error', 'Poll saved, but the list could not refresh right now.', false);
            });
        } catch (error) {
            setAlert(root, 'error', 'Unable to save this poll right now.', true);
        } finally {
            setButtonLoading(submit, false, 'Saving...', 'Save poll');
        }
    }

    async function changeStatus(root, select) {
        const statusUrl = root.getAttribute('data-status-url');
        const pollId = select.getAttribute('data-poll-id');
        if (!statusUrl || !pollId) {
            return;
        }

        try {
            const formData = new FormData();
            formData.append('id', pollId);
            formData.append('status', select.value);
            const response = await fetch(statusUrl, { method: 'POST', body: formData });
            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, 'error', result.message || 'Unable to update poll status.', false);
                return;
            }

            setAlert(root, 'success', result.message || 'Poll status updated successfully.', false);
            await refreshList(root);
        } catch (error) {
            setAlert(root, 'error', 'Unable to update poll status right now.', false);
        }
    }

    async function deletePoll(root, button) {
        const deleteUrl = root.getAttribute('data-delete-url');
        const pollId = button.getAttribute('data-poll-id');
        if (!deleteUrl || !pollId || !window.confirm('Delete this poll?')) {
            return;
        }

        try {
            const formData = new FormData();
            formData.append('id', pollId);
            const response = await fetch(deleteUrl, { method: 'POST', body: formData });
            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, 'error', result.message || 'Unable to delete this poll.', false);
                return;
            }

            setAlert(root, 'success', result.message || 'Poll deleted successfully!', false);
            button.closest('[data-admin-poll-card]')?.remove();
            applyFilters(root);
            refreshList(root).catch(function () {
                setAlert(root, 'error', 'Poll deleted, but the list could not refresh right now.', false);
            });
        } catch (error) {
            setAlert(root, 'error', 'Unable to delete this poll right now.', false);
        }
    }

    async function viewResults(root, button) {
        const detailsUrl = root.getAttribute('data-details-url');
        const pollId = button.getAttribute('data-poll-id');
        if (!detailsUrl || !pollId) {
            return;
        }

        try {
            const separator = detailsUrl.includes('?') ? '&' : '?';
            const response = await fetch(`${detailsUrl}${separator}id=${encodeURIComponent(pollId)}`, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                }
            });
            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, 'error', result.message || 'Unable to load poll results.', false);
                return;
            }

            openResultsModal(root, result.poll);
        } catch (error) {
            setAlert(root, 'error', 'Unable to load poll results right now.', false);
        }
    }

    window.initializeAdminPollsModule = function initializeAdminPollsModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('click', function (event) {
            const openCreate = event.target.closest('[data-admin-polls-open-create]');
            if (openCreate) {
                openCreateModal(root);
                return;
            }

            const close = event.target.closest('[data-admin-polls-close]');
            if (close) {
                closeCreateModal(root);
                return;
            }

            const closeResults = event.target.closest('[data-admin-polls-results-close]');
            if (closeResults) {
                closeResultsModal(root);
                return;
            }

            const addOption = event.target.closest('[data-admin-polls-add-option]');
            if (addOption) {
                addOptionField(root);
                return;
            }

            const removeOption = event.target.closest('[data-admin-polls-remove-option]');
            if (removeOption) {
                removeOption.closest('.admin-polls-option-row')?.remove();
                return;
            }

            const view = event.target.closest('[data-admin-poll-view]');
            if (view) {
                viewResults(root, view);
                return;
            }

            const del = event.target.closest('[data-admin-poll-delete]');
            if (del) {
                deletePoll(root, del);
            }
        });

        root.addEventListener('change', function (event) {
            const select = event.target.closest('[data-admin-poll-status]');
            if (select) {
                changeStatus(root, select);
            }
        });

        root.addEventListener('submit', function (event) {
            const form = event.target.closest('[data-admin-polls-form]');
            if (!form) {
                return;
            }

            event.preventDefault();
            createPoll(root);
        });

        root.querySelector('[data-admin-polls-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        root.querySelector('[data-admin-polls-filter]')?.addEventListener('change', function () {
            applyFilters(root);
        });

        applyFilters(root);

        if (typeof window.__registerAdminEmbeddedCleanup === 'function') {
            window.__registerAdminEmbeddedCleanup(function () {
                document.body.classList.remove('admin-polls-modal-open');
            });
        }
    };
})();
