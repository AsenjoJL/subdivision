(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function applyFilters(root) {
        const query = (root.querySelector('[data-home-event-search]')?.value || '').trim().toLowerCase();
        const selectedCategory = root.dataset.selectedCategory || 'All';
        let visible = 0;

        root.querySelectorAll('[data-home-event-card]').forEach((card) => {
            const searchValue = (card.getAttribute('data-search') || '').toLowerCase();
            const categoryValue = card.getAttribute('data-home-event-category') || '';
            const matchesSearch = !query || searchValue.includes(query);
            const matchesCategory = selectedCategory === 'All' || categoryValue === selectedCategory;
            const shouldShow = matchesSearch && matchesCategory;
            card.classList.toggle('homeowner-events-hidden', !shouldShow);
            if (shouldShow) {
                visible += 1;
            }
        });

        root.querySelectorAll('[data-home-event-category]').forEach((button) => {
            button.classList.toggle('is-active', button.getAttribute('data-home-event-category') === selectedCategory);
        });

        const totalNode = root.querySelector('[data-home-event-total]');
        if (totalNode) {
            totalNode.textContent = visible.toString();
        }

        root.querySelector('[data-home-event-empty]')?.classList.toggle('homeowner-events-hidden', visible > 0);
    }

    const initializeHomeownerEvents = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="events"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="events"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';
        root.dataset.selectedCategory = 'All';

        root.addEventListener('click', function (event) {
            const categoryButton = event.target.closest('[data-home-event-category]');
            if (!categoryButton) {
                return;
            }

            event.preventDefault();
            root.dataset.selectedCategory = categoryButton.getAttribute('data-home-event-category') || 'All';
            applyFilters(root);
        });

        root.querySelector('[data-home-event-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        applyFilters(root);
    };

    window.initializeHomeownerEvents = initializeHomeownerEvents;
    getDashboardModuleInitializers().events = initializeHomeownerEvents;
})();
