(function () {
    function parseNumeric(value) {
        const parsed = Number.parseFloat(String(value).replace(/[^0-9.-]/g, ''));
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function formatValue(value, rawLabel) {
        if ((rawLabel || '').startsWith('Php')) {
            return `Php ${Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
        }

        return Math.round(value).toLocaleString();
    }

    function animateMetricValue(node) {
        const rawValue = node.getAttribute('data-target') || node.textContent || '0';
        const target = parseNumeric(rawValue);
        const start = performance.now();
        const duration = 900;

        function tick(now) {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            node.textContent = formatValue(target * eased, rawValue);

            if (progress < 1) {
                requestAnimationFrame(tick);
            } else {
                node.textContent = rawValue;
            }
        }

        requestAnimationFrame(tick);
    }

    function renderBreakdown(root, key, items) {
        const host = root.querySelector(`[data-analytics-breakdown="${key}"]`);
        if (!host) {
            return;
        }

        const maxValue = Math.max(...items.map((item) => item.value), 1);
        host.innerHTML = items.map((item) => {
            const percentage = Math.max(8, Math.round((item.value / maxValue) * 100));
            return `
                <article class="admin-analytics-breakdown__item" data-tone="${item.tone}">
                    <div class="admin-analytics-breakdown__header">
                        <span>${item.label}</span>
                        <strong>${item.value}</strong>
                    </div>
                    <div class="admin-analytics-breakdown__track">
                        <span class="admin-analytics-breakdown__bar" style="--target:${percentage}%"></span>
                    </div>
                </article>
            `;
        }).join('');
    }

    function renderBars(root, key, items) {
        const host = root.querySelector(`[data-analytics-bars="${key}"]`);
        if (!host) {
            return;
        }

        const maxValue = Math.max(...items.map((item) => item.value), 1);
        host.innerHTML = items.map((item) => {
            const percentage = Math.max(6, Math.round((item.value / maxValue) * 100));
            return `
                <article class="admin-analytics-bar">
                    <div class="admin-analytics-bar__copy">
                        <strong>${item.label}</strong>
                        <span>${item.subtitle || ''}</span>
                    </div>
                    <div class="admin-analytics-bar__track">
                        <span class="admin-analytics-bar__fill" style="--target:${percentage}%"></span>
                    </div>
                    <strong class="admin-analytics-bar__value">${item.value}</strong>
                </article>
            `;
        }).join('');
    }

    function buildLinePath(points, width, height, maxValue) {
        if (!points.length) {
            return '';
        }

        const paddingX = 32;
        const paddingY = 24;
        const usableWidth = width - (paddingX * 2);
        const usableHeight = height - (paddingY * 2);

        return points.map((value, index) => {
            const x = paddingX + ((usableWidth / Math.max(points.length - 1, 1)) * index);
            const y = height - paddingY - ((value / Math.max(maxValue, 1)) * usableHeight);
            return `${index === 0 ? 'M' : 'L'} ${x} ${y}`;
        }).join(' ');
    }

    function renderLineChart(root, chartKey, trend, seriesMap) {
        const chart = root.querySelector(`[data-analytics-line-chart="${chartKey}"]`);
        if (!chart) {
            return;
        }

        const width = 720;
        const height = 280;
        const lineMap = Object.fromEntries(
            Object.entries(seriesMap).map(([key, selector]) => [
                key,
                trend.map((item) => Number(item?.[selector] ?? 0))
            ])
        );
        const maxValue = Math.max(
            ...Object.values(lineMap).flatMap((values) => values),
            1
        );

        Object.entries(lineMap).forEach(([key, values]) => {
            const line = chart.querySelector(`[data-analytics-line="${key}"]`);
            if (line) {
                line.setAttribute('d', buildLinePath(values, width, height, maxValue));
            }

            const pointsHost = chart.querySelector(`[data-analytics-points="${key}"]`);
            if (pointsHost) {
                const paddingX = 32;
                const paddingY = 24;
                const usableWidth = width - (paddingX * 2);
                const usableHeight = height - (paddingY * 2);

                pointsHost.innerHTML = values.map((value, index) => {
                    const x = paddingX + ((usableWidth / Math.max(values.length - 1, 1)) * index);
                    const y = height - paddingY - ((value / Math.max(maxValue, 1)) * usableHeight);
                    return `<circle cx="${x}" cy="${y}" r="4.5"></circle>`;
                }).join('');
            }
        });

        const gridHost = chart.querySelector('[data-analytics-grid-lines]');
        if (gridHost) {
            const lines = 5;
            gridHost.innerHTML = Array.from({ length: lines }).map((_, index) => {
                const y = 24 + (((height - 48) / (lines - 1)) * index);
                return `<line x1="32" y1="${y}" x2="688" y2="${y}"></line>`;
            }).join('');
        }

        const labelsHost = chart.querySelector('[data-analytics-line-labels]');
        if (labelsHost) {
            labelsHost.innerHTML = trend.map((item) => `<span>${item.label}</span>`).join('');
        }

        chart.querySelector('svg')?.classList.add('is-ready');
    }

    window.initializeAdminAnalyticsModule = function initializeAdminAnalyticsModule(root) {
        if (!root || root.dataset.initialized === 'true') {
            return;
        }

        root.dataset.initialized = 'true';
        const payload = JSON.parse(root.getAttribute('data-analytics') || '{}');

        root.querySelectorAll('[data-analytics-metric] .admin-analytics-metric__value').forEach(animateMetricValue);
        renderLineChart(root, 'operations', payload.monthlyTrend || [], {
            reservations: 'reservations',
            requests: 'serviceRequests',
            billings: 'billings'
        });
        renderLineChart(root, 'financial', payload.financialTrend || [], {
            collected: 'collectedRevenue',
            outstanding: 'outstandingRevenue'
        });
        renderBreakdown(root, 'reservations', payload.reservationBreakdown || []);
        renderBreakdown(root, 'billing', payload.billingBreakdown || []);
        renderBreakdown(root, 'serviceRequests', payload.serviceRequestBreakdown || []);
        renderBars(root, 'facilities', payload.facilityPerformance || []);
        renderBars(root, 'serviceCategories', payload.serviceCategoryPerformance || []);
        renderBars(root, 'communitySignals', payload.communitySignals || []);
    };
})();
