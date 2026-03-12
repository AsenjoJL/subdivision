import React, { useEffect, useMemo, useRef, useState } from "https://esm.sh/react@18.3.1";
import { createRoot } from "https://esm.sh/react-dom@18.3.1/client";
import htm from "https://esm.sh/htm@3.1.1";
import Chart from "https://esm.sh/chart.js@4.4.0/auto";

const html = htm.bind(React.createElement);
const config = window.adminDashboardConfig || {};
const links = config.links || {};
const workspace = config.workspace || {};
window.__adminEmbeddedCleanups = window.__adminEmbeddedCleanups || [];
window.__registerAdminEmbeddedCleanup = function registerAdminEmbeddedCleanup(cleanup) {
    if (typeof cleanup === "function") {
        window.__adminEmbeddedCleanups.push(cleanup);
    }
};

const navGroups = [
    {
        label: "Overview",
        items: [
            { key: "dashboard", label: "Dashboard", icon: "fa-home", type: "dashboard", href: links.dashboard }
        ]
    },
    {
        label: "User management",
        items: [
            { key: "homeowners", label: "Homeowners", icon: "fa-users", type: "ajax", href: links.manageOwners },
            { key: "staff", label: "Staff", icon: "fa-user-tie", type: "ajax", href: links.manageStaff }
        ]
    },
    {
        label: "Service management",
        items: [
            { key: "serviceRequests", label: "Service requests", icon: "fa-tools", type: "ajax", href: links.manageServiceRequests },
            { key: "reservations", label: "Reservations", icon: "fa-calendar-check", type: "ajax", href: links.reservationManagement },
            { key: "complaints", label: "Complaints", icon: "fa-exclamation-circle", type: "ajax", href: links.complaints }
        ]
    },
    {
        label: "Content management",
        items: [
            { key: "announcements", label: "Announcements", icon: "fa-bullhorn", type: "ajax", href: links.announcements },
            { key: "events", label: "Events", icon: "fa-calendar-alt", type: "ajax", href: links.events },
            { key: "forum", label: "Community forum", icon: "fa-comments", type: "ajax", href: links.forum },
            { key: "documents", label: "Documents", icon: "fa-file-alt", type: "ajax", href: links.documents },
            { key: "polls", label: "Polls & surveys", icon: "fa-poll", type: "ajax", href: links.polls }
        ]
    },
    {
        label: "Community management",
        items: [
            { key: "contacts", label: "Contact directory", icon: "fa-address-book", type: "ajax", href: links.contacts },
            { key: "visitorPasses", label: "Visitor passes", icon: "fa-id-card", type: "ajax", href: links.visitorPasses },
            { key: "vehicleRegistration", label: "Vehicle registration", icon: "fa-car", type: "ajax", href: links.vehicleRegistration },
            { key: "gateAccessLogs", label: "Gate access logs", icon: "fa-door-open", type: "ajax", href: links.gateAccessLogs }
        ]
    },
    {
        label: "Financial management",
        items: [
            { key: "billing", label: "Billing & payments", icon: "fa-money-bill-wave", type: "ajax", href: links.billing }
        ]
    },
    {
        label: "Reports & analytics",
        items: [
            { key: "analytics", label: "Analytics dashboard", icon: "fa-chart-bar", type: "ajax", href: links.analytics }
        ]
    },
    {
        label: "Account",
        items: [
            { key: "logout", label: "Logout", icon: "fa-sign-out-alt", type: "link", href: links.logout }
        ]
    }
];

const statDefinitions = [
    {
        key: "homeownerCount",
        label: "Homeowners",
        icon: "fa-users",
        toneAccent: "#2563eb",
        toneWash: "radial-gradient(circle, rgba(37, 99, 235, 0.12) 0%, rgba(37, 99, 235, 0) 72%)",
        toneIcon: "rgba(37, 99, 235, 0.12)",
        meta: "Resident records synced",
        statusTone: "green"
    },
    {
        key: "staffCount",
        label: "Staff",
        icon: "fa-user-tie",
        toneAccent: "#7257ff",
        toneWash: "radial-gradient(circle, rgba(114, 87, 255, 0.12) 0%, rgba(114, 87, 255, 0) 72%)",
        toneIcon: "rgba(114, 87, 255, 0.11)",
        meta: "Operations team online",
        statusTone: "green"
    },
    {
        key: "reservationCount",
        label: "Reservations",
        icon: "fa-calendar-check",
        toneAccent: "#0f9d94",
        toneWash: "radial-gradient(circle, rgba(15, 157, 148, 0.12) 0%, rgba(15, 157, 148, 0) 72%)",
        toneIcon: "rgba(15, 157, 148, 0.11)",
        meta: "Approved bookings in queue",
        statusTone: "amber"
    },
    {
        key: "notificationCount",
        label: "Notifications",
        icon: "fa-bell",
        toneAccent: "#c98a0b",
        toneWash: "radial-gradient(circle, rgba(201, 138, 11, 0.14) 0%, rgba(201, 138, 11, 0) 72%)",
        toneIcon: "rgba(201, 138, 11, 0.12)",
        meta: "Unread operational alerts",
        statusTone: "amber"
    }
];

const activityThemeMap = {
    homeowner: { color: "#2b8cff", icon: "fa-user-plus" },
    staff: { color: "#4ac0ff", icon: "fa-user-gear" },
    reservation: { color: "#6aa4ff", icon: "fa-calendar-check" },
    serviceRequest: { color: "#2dd4a0", icon: "fa-screwdriver-wrench" },
    announcement: { color: "#f8b84e", icon: "fa-bullhorn" },
    urgentAnnouncement: { color: "#ff7f50", icon: "fa-triangle-exclamation" }
};

function getInitials(name) {
    return (name || "Admin profile")
        .split(" ")
        .filter(Boolean)
        .slice(0, 2)
        .map((part) => part[0]?.toUpperCase())
        .join("");
}

function useCountUp(target, duration = 900) {
    const [value, setValue] = useState(0);

    useEffect(() => {
        let rafId = 0;
        const started = performance.now();
        const numericTarget = Number.isFinite(target) ? target : 0;

        const tick = (now) => {
            const progress = Math.min((now - started) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            setValue(Math.round(numericTarget * eased));
            if (progress < 1) {
                rafId = requestAnimationFrame(tick);
            }
        };

        setValue(0);
        rafId = requestAnimationFrame(tick);
        return () => cancelAnimationFrame(rafId);
    }, [target, duration]);

    return value;
}

function executeEmbeddedScripts(container) {
    const scripts = container.querySelectorAll("script");
    scripts.forEach((script) => {
        const replacement = document.createElement("script");
        Array.from(script.attributes).forEach((attribute) => {
            replacement.setAttribute(attribute.name, attribute.value);
        });
        replacement.textContent = script.textContent;
        script.parentNode?.replaceChild(replacement, script);
    });
}

function cleanupEmbeddedContent() {
    if (Array.isArray(window.__adminEmbeddedCleanups)) {
        while (window.__adminEmbeddedCleanups.length > 0) {
            const cleanup = window.__adminEmbeddedCleanups.pop();
            try {
                cleanup?.();
            } catch (error) {
                console.error(error);
            }
        }
    }

    const legacyCleanupKeys = ["__ownerModalCleanup", "__staffManagementCleanup"];
    legacyCleanupKeys.forEach((key) => {
        if (typeof window[key] === "function") {
            try {
                window[key]();
            } catch (error) {
                console.error(error);
            } finally {
                window[key] = null;
            }
        }
    });
}

function findNavItemByKey(key) {
    for (const group of navGroups) {
        const match = group.items.find((item) => item.key === key);
        if (match) {
            return match;
        }
    }

    return navGroups[0].items[0];
}

function StatCard({ stat, value, delay }) {
    const count = useCountUp(value);

    return html`
        <article
            className="glass-card stat-card"
            style=${{
                "--delay": `${delay}ms`,
                "--card-accent": stat.toneAccent,
                "--card-wash": stat.toneWash,
                "--card-icon-bg": stat.toneIcon
            }}
        >
            <span className="stat-accent"></span>
            <div className="stat-topline">
                <p className="stat-label">${stat.label}</p>
                <div className="stat-icon">
                    <i className=${`fas ${stat.icon}`}></i>
                </div>
            </div>
            <div>
                <div className="stat-number">${count}</div>
                <div className="stat-pill">
                    <span className=${`status-dot ${stat.statusTone === "green" ? "is-green" : "is-amber"}`}></span>
                    <span>${stat.meta}</span>
                </div>
            </div>
        </article>
    `;
}

function AnalyticsCard({ facilityUsage }) {
    const canvasRef = useRef(null);
    const labels = facilityUsage.length > 0
        ? facilityUsage.map((item) => item.label)
        : ["No facility data"];
    const values = facilityUsage.length > 0
        ? facilityUsage.map((item) => item.value)
        : [0];

    useEffect(() => {
        if (!canvasRef.current) {
            return undefined;
        }

        const chart = new Chart(canvasRef.current, {
            type: "bar",
            devicePixelRatio: Math.max(window.devicePixelRatio || 1, 2),
            data: {
                labels,
                datasets: [
                    {
                        label: "Usage level",
                        data: values,
                        backgroundColor: [
                            "#2f6ff6",
                            "#5b86f7",
                            "#89a8fa",
                            "#b2c4fb",
                            "#d7e1fe"
                        ],
                        borderRadius: 16,
                        borderSkipped: false,
                        maxBarThickness: 58
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: {
                    duration: 700,
                    easing: "easeOutQuart"
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: "rgba(255, 255, 255, 0.98)",
                        titleColor: "#10213a",
                        bodyColor: "#4f617c",
                        borderColor: "rgba(18, 35, 60, 0.08)",
                        borderWidth: 1,
                        displayColors: false,
                        padding: 14,
                        cornerRadius: 12
                    }
                },
                scales: {
                    x: {
                        ticks: {
                            color: "#31435f",
                            padding: 12,
                            maxRotation: 0,
                            font: {
                                family: "Manrope",
                                size: 12,
                                weight: "600"
                            }
                        },
                        grid: { display: false },
                        border: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        max: 100,
                        ticks: {
                            color: "#51627d",
                            padding: 10,
                            stepSize: 20,
                            font: {
                                family: "Manrope",
                                size: 11,
                                weight: "600"
                            }
                        },
                        grid: {
                            color: "rgba(18, 35, 60, 0.1)",
                            drawBorder: false
                        },
                        border: { display: false }
                    }
                }
            }
        });

        requestAnimationFrame(() => chart.resize());
        return () => chart.destroy();
    }, [labels, values]);

    return html`
        <section className="glass-card section-card analytics-card stagger-in" style=${{ "--delay": "140ms" }}>
            <div className="section-header">
                <div>
                    <h2 className="section-heading">Facility usage analytics</h2>
                    <p className="section-caption">
                        ${facilityUsage.length > 0
                            ? "Live booking volume across the most requested amenities."
                            : "Approved reservation activity will appear here once facilities are used."}
                    </p>
                </div>
            </div>
            <div className="chart-wrap">
                <canvas ref=${canvasRef}></canvas>
            </div>
        </section>
    `;
}

function ActivityFeed({ items }) {
    const activityItems = items.length > 0 ? items : [];

    return html`
        <section className="glass-card section-card stagger-in" style=${{ "--delay": "220ms" }}>
            <div className="section-header">
                <div>
                    <h2 className="section-heading">Recent activity</h2>
                    <p className="section-caption">Live operational updates flowing in from the admin workspace.</p>
                </div>
            </div>
            ${activityItems.length === 0
                ? html`
                    <div className="activity-item" style=${{ "--activity-color": "#2b8cff" }}>
                        <div className="activity-row">
                            <div className="activity-icon">
                                <i className="fas fa-wave-square"></i>
                            </div>
                            <div>
                                <p className="activity-title">No recent activity yet</p>
                                <p className="activity-description">As admins create records and approve actions, updates will appear here.</p>
                            </div>
                        </div>
                    </div>
                `
                : html`
                    <div className="activity-list">
                        ${activityItems.map((item, index) => {
                            const theme = activityThemeMap[item.type] || activityThemeMap.announcement;
                            return html`
                                <article
                                    key=${`${item.type}-${item.title}-${index}`}
                                    className="activity-item"
                                    style=${{ "--activity-color": theme.color, animationDelay: `${index * 70}ms` }}
                                >
                                    <div className="activity-row">
                                        <div className="activity-icon">
                                            <i className=${`fas ${theme.icon}`}></i>
                                        </div>
                                        <div>
                                            <p className="activity-title">${item.title}</p>
                                            <p className="activity-description">${item.description}</p>
                                            <div className="activity-time">${item.timeAgo}</div>
                                        </div>
                                    </div>
                                </article>
                            `;
                        })}
                    </div>
                `}
        </section>
    `;
}

function DashboardHome({ stats, facilityUsage, recentActivity }) {
    return html`
        <div className="section-stack">
            <section className="stat-grid">
                ${statDefinitions.map(
                    (stat, index) => html`
                        <${StatCard}
                            key=${stat.key}
                            stat=${stat}
                            value=${stats[stat.key] ?? 0}
                            delay=${index * 90}
                        />
                    `
                )}
            </section>

            <section className="dashboard-row">
                <${AnalyticsCard} facilityUsage=${facilityUsage} />
                <${ActivityFeed} items=${recentActivity} />
            </section>
        </div>
    `;
}

function Sidebar({ activeKey, onSelect, isOpen }) {
    return html`
        <aside className=${`admin-sidebar ${isOpen ? "is-open" : ""}`}>
            <div className="brand-block">
                <div className="brand-mark">
                    <i className="fas fa-shield-halved"></i>
                </div>
                <div className="brand-copy">
                    <h1 className="brand-title">RestNestHome</h1>
                    <p className="brand-subtitle">Administrative control center</p>
                </div>
            </div>

            ${navGroups.map(
                (group) => html`
                    <section key=${group.label} className="menu-group">
                        <p className="menu-label">${group.label}</p>
                        <div className="menu-stack">
                            ${group.items.map(
                                (item) => html`
                                    <button
                                        key=${item.key}
                                        type="button"
                                        className=${`menu-item ${activeKey === item.key ? "is-active" : ""}`}
                                        onClick=${() => onSelect(item)}
                                    >
                                        <span className="menu-icon">
                                            <i className=${`fas ${item.icon}`}></i>
                                        </span>
                                        <span className="menu-text">${item.label}</span>
                                        <span className="menu-arrow">
                                            <i className=${item.type === "link" ? "fas fa-arrow-right" : "fas fa-angle-right"}></i>
                                        </span>
                                    </button>
                                `
                            )}
                        </div>
                    </section>
                `
            )}
        </aside>
    `;
}

function TopBar({ adminName, workspaceName, dropdownOpen, onToggleDropdown, onToggleMobile, onOpenProfile, onOpenWorkspaceSettings }) {
    const initials = useMemo(() => getInitials(adminName), [adminName]);

    return html`
        <header className="admin-topbar stagger-in" style=${{ "--delay": "40ms" }}>
            <div style=${{ display: "flex", alignItems: "center", gap: "16px" }}>
                <button type="button" className="mobile-toggle" onClick=${onToggleMobile}>
                    <i className="fas fa-bars"></i>
                </button>
                <div className="topbar-copy">
                    <h1>Admin dashboard</h1>
                    <p>Community operations, resident activity, and service visibility in one place.</p>
                </div>
            </div>

            <div className="profile-cluster">
                <button type="button" className="profile-button" onClick=${onToggleDropdown}>
                    <div style=${{ display: "flex", alignItems: "center", gap: "14px" }}>
                        <div className="profile-avatar">${initials}</div>
                        <div className="profile-meta">
                            <span className="profile-label">${workspaceName || "Admin workspace"}</span>
                            <span className="profile-name">${adminName}</span>
                        </div>
                    </div>
                    <i className="fas fa-chevron-down"></i>
                </button>

                ${dropdownOpen &&
                html`
                    <div className="profile-dropdown">
                        <button type="button" onClick=${onOpenProfile}>
                            <i className="fas fa-user-pen"></i>
                            <span>Update profile</span>
                        </button>
                        <button type="button" onClick=${onOpenWorkspaceSettings}>
                            <i className="fas fa-gear"></i>
                            <span>Workspace settings</span>
                        </button>
                        <a href=${links.logout}>
                            <i className="fas fa-right-from-bracket"></i>
                            <span>Logout</span>
                        </a>
                    </div>
                `}
            </div>
        </header>
    `;
}

function ExternalContent({ status, htmlContent, errorMessage, openUrl }) {
    const contentRef = useRef(null);

    useEffect(() => {
        if (status === "loaded" && contentRef.current) {
            cleanupEmbeddedContent();
            executeEmbeddedScripts(contentRef.current);
        }

        return () => {
            cleanupEmbeddedContent();
        };
    }, [status, htmlContent]);

    if (status === "loading") {
        return html`
            <div className="glass-card section-card external-panel panel-loading">
                <div>
                    <div className="panel-spinner"></div>
                    <div>Loading module...</div>
                </div>
            </div>
        `;
    }

    if (status === "error") {
        return html`
            <div className="glass-card section-card external-panel panel-error">
                <div>
                    <h3 className="section-heading">Unable to load this section</h3>
                    <p className="section-caption">${errorMessage}</p>
                </div>
            </div>
        `;
    }

    if (status === "external-page") {
        return html`
            <div className="glass-card section-card external-panel panel-info">
                <div>
                    <h3 className="section-heading">This section opens as a full page</h3>
                    <p className="section-caption">The requested screen returns a full page instead of a partial.</p>
                    ${openUrl &&
                    html`
                        <p style=${{ marginTop: "16px" }}>
                            <a href=${openUrl}>Open the full page</a>
                        </p>
                    `}
                </div>
            </div>
        `;
    }

    return html`
        <div
            ref=${contentRef}
            className="glass-card section-card external-panel"
            dangerouslySetInnerHTML=${{ __html: htmlContent }}
        ></div>
    `;
}

function App() {
    const initialSection = findNavItemByKey(workspace.defaultLandingSection || "dashboard");
    const profileItem = useMemo(() => ({ key: "adminProfile", label: "Update profile", icon: "fa-user-pen", type: "ajax", href: links.adminProfile }), []);
    const workspaceItem = useMemo(() => ({ key: "workspaceSettings", label: "Workspace settings", icon: "fa-gear", type: "ajax", href: links.workspaceSettings }), []);
    const [activeItem, setActiveItem] = useState(initialSection);
    const [adminName, setAdminName] = useState(config.adminName || "Admin profile");
    const [workspaceName, setWorkspaceName] = useState(workspace.workspaceName || "Admin workspace");
    const [useCompactTables, setUseCompactTables] = useState(Boolean(workspace.useCompactTables));
    const [enableSectionPrefetch, setEnableSectionPrefetch] = useState(workspace.enableSectionPrefetch !== false);
    const [overview, setOverview] = useState({
        homeownerCount: 0,
        staffCount: 0,
        reservationCount: 0,
        notificationCount: 0,
        facilityUsage: [],
        recentActivity: []
    });
    const [contentState, setContentState] = useState({
        status: "idle",
        htmlContent: "",
        errorMessage: "",
        openUrl: ""
    });
    const [dropdownOpen, setDropdownOpen] = useState(false);
    const [mobileOpen, setMobileOpen] = useState(false);

    useEffect(() => {
        let cancelled = false;

        async function loadStats() {
            const overviewUrl = links.dashboardOverview || links.dashboardStats;
            if (!overviewUrl) {
                return;
            }

            try {
                const response = await fetch(overviewUrl, {
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    credentials: "same-origin"
                });

                if (!response.ok) {
                    throw new Error("Failed to load dashboard statistics.");
                }

                const payload = await response.json();
                if (!cancelled) {
                    setOverview({
                        homeownerCount: payload.homeownerCount ?? 0,
                        staffCount: payload.staffCount ?? 0,
                        reservationCount: payload.reservationCount ?? 0,
                        notificationCount: payload.notificationCount ?? 0,
                        facilityUsage: Array.isArray(payload.facilityUsage) ? payload.facilityUsage : [],
                        recentActivity: Array.isArray(payload.recentActivity) ? payload.recentActivity : []
                    });
                }
            } catch (error) {
                if (!cancelled) {
                    console.error(error);
                    setOverview({
                        homeownerCount: 0,
                        staffCount: 0,
                        reservationCount: 0,
                        notificationCount: 0,
                        facilityUsage: [],
                        recentActivity: []
                    });
                }
            }
        }

        loadStats();
        return () => {
            cancelled = true;
        };
    }, []);

    useEffect(() => {
        const handleEscape = (event) => {
            if (event.key === "Escape") {
                setDropdownOpen(false);
                setMobileOpen(false);
            }
        };

        document.addEventListener("keydown", handleEscape);
        return () => document.removeEventListener("keydown", handleEscape);
    }, []);

    useEffect(() => {
        if (initialSection.type !== "dashboard") {
            setActiveItem(initialSection);
            loadExternalSection(initialSection);
        }
    }, []);

    useEffect(() => {
        const handleProfileUpdated = (event) => {
            if (event.detail?.adminName) {
                setAdminName(event.detail.adminName);
                config.adminName = event.detail.adminName;
            }
        };

        const handleWorkspaceUpdated = (event) => {
            const nextSettings = event.detail || {};
            if (nextSettings.workspaceName) {
                setWorkspaceName(nextSettings.workspaceName);
                workspace.workspaceName = nextSettings.workspaceName;
            }

            if (typeof nextSettings.useCompactTables === "boolean") {
                setUseCompactTables(nextSettings.useCompactTables);
                workspace.useCompactTables = nextSettings.useCompactTables;
            }

            if (typeof nextSettings.enableSectionPrefetch === "boolean") {
                setEnableSectionPrefetch(nextSettings.enableSectionPrefetch);
                workspace.enableSectionPrefetch = nextSettings.enableSectionPrefetch;
            }

            if (nextSettings.defaultLandingSection) {
                workspace.defaultLandingSection = nextSettings.defaultLandingSection;
            }
        };

        window.addEventListener("admin-profile-updated", handleProfileUpdated);
        window.addEventListener("admin-workspace-updated", handleWorkspaceUpdated);
        return () => {
            window.removeEventListener("admin-profile-updated", handleProfileUpdated);
            window.removeEventListener("admin-workspace-updated", handleWorkspaceUpdated);
        };
    }, []);

    useEffect(() => {
        if (!enableSectionPrefetch) {
            return undefined;
        }

        let cancelled = false;
        const prefetchItems = [findNavItemByKey("homeowners"), findNavItemByKey("staff"), findNavItemByKey("reservations")].filter((item) => item?.href);
        const timerId = window.setTimeout(() => {
            prefetchItems.forEach((item, index) => {
                window.setTimeout(() => {
                    if (cancelled) {
                        return;
                    }

                    fetch(item.href, {
                        headers: { "X-Requested-With": "XMLHttpRequest" },
                        credentials: "same-origin"
                    }).catch((error) => console.error(error));
                }, index * 180);
            });
        }, 600);

        return () => {
            cancelled = true;
            window.clearTimeout(timerId);
        };
    }, [enableSectionPrefetch]);

    async function loadExternalSection(item) {
        if (!item.href) {
            setContentState({
                status: "error",
                htmlContent: "",
                errorMessage: "This navigation item is missing a destination.",
                openUrl: ""
            });
            return;
        }

        setContentState({
            status: "loading",
            htmlContent: "",
            errorMessage: "",
            openUrl: item.href
        });

        try {
            const response = await fetch(item.href, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error("The section could not be loaded.");
            }

            const htmlContent = await response.text();
            if (/<html[\s>]/i.test(htmlContent) || /<body[\s>]/i.test(htmlContent)) {
                setContentState({
                    status: "external-page",
                    htmlContent: "",
                    errorMessage: "",
                    openUrl: item.href
                });
                return;
            }

            setContentState({
                status: "loaded",
                htmlContent,
                errorMessage: "",
                openUrl: item.href
            });
        } catch (error) {
            console.error(error);
            setContentState({
                status: "error",
                htmlContent: "",
                errorMessage: "The section could not be loaded right now. Please try again.",
                openUrl: item.href
            });
        }
    }

    function handleSelect(item) {
        if (item.type === "link") {
            window.location.href = item.href;
            return;
        }

        setDropdownOpen(false);
        setMobileOpen(false);
        setActiveItem(item);

        if (item.type === "dashboard") {
            setContentState({
                status: "idle",
                htmlContent: "",
                errorMessage: "",
                openUrl: ""
            });
            return;
        }

        loadExternalSection(item);
    }

    return html`
        <div className="admin-shell">
            ${mobileOpen && html`<button type="button" className="mobile-overlay" onClick=${() => setMobileOpen(false)}></button>`}
            <${Sidebar}
                activeKey=${activeItem.key}
                onSelect=${handleSelect}
                isOpen=${mobileOpen}
            />

            <main className=${`admin-main ${useCompactTables ? "admin-main--compact" : ""}`}>
                <${TopBar}
                    adminName=${adminName}
                    workspaceName=${workspaceName}
                    dropdownOpen=${dropdownOpen}
                    onToggleDropdown=${() => setDropdownOpen((current) => !current)}
                    onToggleMobile=${() => setMobileOpen((current) => !current)}
                    onOpenProfile=${() => handleSelect(profileItem)}
                    onOpenWorkspaceSettings=${() => handleSelect(workspaceItem)}
                />

                <section className="content-frame">
                    ${activeItem.type === "dashboard"
                        ? html`
                            <${DashboardHome}
                                stats=${overview}
                                facilityUsage=${overview.facilityUsage}
                                recentActivity=${overview.recentActivity}
                            />
                        `
                        : html`
                            <${ExternalContent}
                                status=${contentState.status}
                                htmlContent=${contentState.htmlContent}
                                errorMessage=${contentState.errorMessage}
                                openUrl=${contentState.openUrl}
                            />
                        `}
                </section>
            </main>
        </div>
    `;
}

const mountNode = document.getElementById("adminDashboardRoot");

if (mountNode) {
    createRoot(mountNode).render(html`<${App} />`);
}
