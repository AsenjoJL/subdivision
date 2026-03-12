(function () {
    function getDashboardModuleInitializers() {
        window.homeownerDashboardModuleInitializers = window.homeownerDashboardModuleInitializers || {};
        return window.homeownerDashboardModuleInitializers;
    }

    function normalize(value) {
        return (value || '').toString().trim().toLowerCase();
    }

    function setVisibleCount(root, count) {
        root.querySelectorAll('[data-doc-visible-count]').forEach((node) => {
            node.textContent = count.toString();
        });
    }

    function applyFilters(root) {
        const selectedCategory = root.dataset.selectedCategory || 'All';
        const query = normalize(root.querySelector('[data-doc-search]')?.value);
        const cards = Array.from(root.querySelectorAll('[data-document-card]'));
        let visibleCount = 0;

        cards.forEach((card) => {
            const matchesCategory = selectedCategory === 'All' || card.getAttribute('data-category') === selectedCategory;
            const matchesSearch = !query || normalize(card.getAttribute('data-search')).includes(query);
            const isVisible = matchesCategory && matchesSearch;

            card.classList.toggle('document-library-hidden', !isVisible);
            if (isVisible) {
                visibleCount += 1;
            }
        });

        root.querySelectorAll('[data-doc-category]').forEach((button) => {
            button.classList.toggle('is-active', button.getAttribute('data-doc-category') === selectedCategory);
        });

        const emptyState = root.querySelector('[data-doc-empty]');
        emptyState?.classList.toggle('document-library-hidden', visibleCount > 0);

        setVisibleCount(root, visibleCount);
    }

    const initializeHomeownerDocuments = function (contentRoot) {
        const root = contentRoot.querySelector('[data-homeowner-module="documents"]')
            || (contentRoot.matches && contentRoot.matches('[data-homeowner-module="documents"]') ? contentRoot : null);

        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';

        root.addEventListener('click', function (event) {
            const categoryButton = event.target.closest('[data-doc-category]');
            if (!categoryButton) {
                return;
            }

            event.preventDefault();
            root.dataset.selectedCategory = categoryButton.getAttribute('data-doc-category') || 'All';
            applyFilters(root);
        });

        root.querySelector('[data-doc-search]')?.addEventListener('input', function () {
            applyFilters(root);
        });

        applyFilters(root);
    };

    window.initializeHomeownerDocuments = initializeHomeownerDocuments;
    getDashboardModuleInitializers().documents = initializeHomeownerDocuments;
})();
