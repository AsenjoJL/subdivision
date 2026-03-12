(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function setAlert(root, type, message) {
        const success = root.querySelector('[data-home-polls-success]');
        const error = root.querySelector('[data-home-polls-error]');
        const target = type === 'success' ? success : error;
        const other = type === 'success' ? error : success;

        if (other) {
            other.classList.add('homeowner-polls-hidden');
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
            target.classList.add('homeowner-polls-hidden');
            window.clearTimeout(target.__hideTimer);
            return;
        }

        target.classList.remove('homeowner-polls-hidden');
        window.clearTimeout(target.__hideTimer);
        target.__hideTimer = window.setTimeout(function () {
            target.classList.add('homeowner-polls-hidden');
        }, 3200);
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-home-polls-search]')?.value || '').trim().toLowerCase();
        let visible = 0;

        root.querySelectorAll('[data-home-poll-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const matches = !query || searchValue.includes(query);
            card.classList.toggle('homeowner-polls-hidden', !matches);
            if (matches) {
                visible += 1;
            }
        });

        const empty = root.querySelector('[data-home-polls-empty]');
        if (empty && root.querySelectorAll('[data-home-poll-card]').length > 0) {
            empty.classList.toggle('homeowner-polls-hidden', visible > 0);
        }

        const total = root.querySelector('[data-home-polls-total]');
        if (total) {
            total.textContent = visible.toString();
        }
    }

    async function refreshList(root) {
        const host = root.querySelector('[data-home-polls-list-host]');
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

    async function vote(root, button) {
        const pollId = button.getAttribute('data-poll-id');
        const voteUrl = root.getAttribute('data-vote-url');
        const selected = root.querySelector(`input[data-home-poll-option="${pollId}"]:checked`);
        if (!pollId || !voteUrl) {
            return;
        }

        if (!selected) {
            setAlert(root, 'error', 'Select an option before submitting your vote.');
            return;
        }

        setAlert(root, 'success', '');
        setAlert(root, 'error', '');
        button.disabled = true;

        try {
            const formData = new FormData();
            formData.append('pollId', pollId);
            formData.append('optionId', selected.value);

            const response = await fetch(voteUrl, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                setAlert(root, 'error', result.message || 'Unable to submit your vote.');
                return;
            }

            await refreshList(root);
            setAlert(root, 'success', result.message || 'Vote submitted successfully!');
        } catch (error) {
            setAlert(root, 'error', 'Unable to submit your vote right now. Please try again.');
        } finally {
            button.disabled = false;
        }
    }

    const initializeHomeownerPollsModule = function (contentRoot) {
        const root = contentRoot.querySelector('[data-home-polls-module]')
            || (contentRoot.matches && contentRoot.matches('[data-home-polls-module]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('click', function (event) {
            const voteButton = event.target.closest('[data-home-poll-vote]');
            if (voteButton) {
                vote(root, voteButton);
            }
        });

        root.querySelector('[data-home-polls-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        applyFilters(root);
    };

    window.initializeHomeownerPollsModule = initializeHomeownerPollsModule;
    getDashboardModuleInitializers().polls = initializeHomeownerPollsModule;
})();
