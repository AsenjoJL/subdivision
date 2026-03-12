import React, { useEffect, useMemo, useState } from "https://esm.sh/react@18.3.1";
import { createRoot } from "https://esm.sh/react-dom@18.3.1/client";
import htm from "https://esm.sh/htm@3.1.1";

const html = htm.bind(React.createElement);

function parseConfig(rootElement) {
    const raw = rootElement.dataset.config;
    return raw ? JSON.parse(raw) : {};
}

function getTodayLocalDate() {
    const now = new Date();
    const timezoneOffset = now.getTimezoneOffset() * 60000;
    return new Date(now.getTime() - timezoneOffset).toISOString().slice(0, 10);
}

function toMinutes(timeText) {
    const [hours, minutes] = timeText.split(":").map(Number);
    return hours * 60 + minutes;
}

function fromParts(parts) {
    if (!parts?.hour || !parts?.minute || !parts?.period) {
        return null;
    }

    let hours = Number(parts.hour);
    const minutes = Number(parts.minute);
    if (parts.period === "PM" && hours !== 12) {
        hours += 12;
    }
    if (parts.period === "AM" && hours === 12) {
        hours = 0;
    }

    return hours * 60 + minutes;
}

function toTimeSpan(minutes) {
    const hours = Math.floor(minutes / 60).toString().padStart(2, "0");
    const mins = (minutes % 60).toString().padStart(2, "0");
    return `${hours}:${mins}:00`;
}

function formatMinutes(minutes) {
    const hours24 = Math.floor(minutes / 60);
    const mins = minutes % 60;
    const period = hours24 >= 12 ? "PM" : "AM";
    let hours12 = hours24 % 12;
    if (hours12 === 0) {
        hours12 = 12;
    }
    return `${hours12}:${mins.toString().padStart(2, "0")} ${period}`;
}

function formatDate(dateText) {
    const date = new Date(`${dateText}T00:00:00`);
    return new Intl.DateTimeFormat("en-US", { month: "long", day: "numeric", year: "numeric" }).format(date);
}

function formatShortDate(dateText) {
    const date = new Date(`${dateText}T00:00:00`);
    return new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric" }).format(date);
}

function durationLabel(startMinutes, endMinutes) {
    if (startMinutes == null || endMinutes == null || endMinutes <= startMinutes) {
        return "";
    }

    const totalMinutes = endMinutes - startMinutes;
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;

    if (hours && minutes) {
        return `${hours} hour${hours === 1 ? "" : "s"} ${minutes} min`;
    }
    if (hours) {
        return `${hours} hour${hours === 1 ? "" : "s"}`;
    }
    return `${minutes} min`;
}

function isOverlap(startA, endA, startB, endB) {
    return startA < endB && endA > startB;
}

function getSlotState(totalBookedMinutes) {
    if (totalBookedMinutes >= 12 * 60) {
        return { label: "Fully Booked", className: "full" };
    }
    if (totalBookedMinutes > 0) {
        return { label: "Partially Booked", className: "partial" };
    }
    return { label: "Available", className: "available" };
}

function formatRatingLabel(value) {
    if (!Number.isFinite(value) || value <= 0) {
        return "No ratings yet";
    }

    return value.toFixed(1);
}

function normalizeSlot(slot) {
    return {
        ...slot,
        startMinutes: toMinutes(slot.start),
        endMinutes: toMinutes(slot.end)
    };
}

function TimePicker({ label, value, onChange, bookedSlots, otherMinutes, mode }) {
    const minuteDisabled = minute => {
        if (!value.hour || !value.period) {
            return false;
        }

        const minutes = fromParts({ hour: value.hour, minute, period: value.period });
        const overlapsBooked = bookedSlots.some(slot => minutes >= slot.startMinutes && minutes < slot.endMinutes);
        if (mode === "end" && otherMinutes != null) {
            return minutes <= otherMinutes || bookedSlots.some(slot => isOverlap(otherMinutes, minutes, slot.startMinutes, slot.endMinutes));
        }

        return overlapsBooked;
    };

    return html`
        <div className="reservation-react-field">
            <label>${label}</label>
            <div className="reservation-react-time-grid">
                <select value=${value.hour} onChange=${event => onChange({ ...value, hour: event.target.value })}>
                    <option value="">Hour</option>
                    ${Array.from({ length: 12 }, (_, index) => index + 1).map(hour => html`
                        <option key=${hour} value=${String(hour)}>${hour}</option>
                    `)}
                </select>
                <select value=${value.minute} onChange=${event => onChange({ ...value, minute: event.target.value })}>
                    <option value="">Minute</option>
                    ${["00", "30"].map(minute => html`
                        <option key=${minute} value=${minute} disabled=${minuteDisabled(minute)}>
                            ${minute}${minuteDisabled(minute) ? " · Booked" : ""}
                        </option>
                    `)}
                </select>
                <select value=${value.period} onChange=${event => onChange({ ...value, period: event.target.value })}>
                    ${["AM", "PM"].map(period => html`<option key=${period} value=${period}>${period}</option>`)}
                </select>
            </div>
        </div>
    `;
}

function ReservationApp({ config }) {
    const initialDate = config.todayDate || getTodayLocalDate();
    const [activityCount, setActivityCount] = useState(() => Number(config.activityCount || 0));
    const [facilities] = useState(() => (config.facilities || []).map(facility => ({
        ...facility,
        ratingValue: Number(facility.rating ?? 0)
    })));
    const [reservedSlotsByFacility, setReservedSlotsByFacility] = useState(() =>
        Object.fromEntries(
            (config.facilities || []).map(facility => [
                facility.id,
                (facility.reservedSlots || []).map(normalizeSlot)
            ])
        )
    );
    const [modalFacilityId, setModalFacilityId] = useState(null);
    const [step, setStep] = useState(1);
    const [banner, setBanner] = useState(() => {
        if (config.successMessage) {
            return { type: "success", message: config.successMessage };
        }
        if (config.errorMessage) {
            return { type: "error", message: config.errorMessage };
        }
        return null;
    });
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [loadingReservedTimes, setLoadingReservedTimes] = useState({});
    const [showReservedTimes, setShowReservedTimes] = useState({});
    const [formState, setFormState] = useState({
        reservationDate: initialDate,
        startTime: { hour: "", minute: "", period: "AM" },
        endTime: { hour: "", minute: "", period: "AM" },
        purpose: ""
    });
    const [errors, setErrors] = useState({});
    const selectedFacility = facilities.find(facility => facility.id === modalFacilityId) || null;
    const totalReservedSlots = useMemo(
        () => Object.values(reservedSlotsByFacility).reduce((total, slots) => total + (slots?.length || 0), 0),
        [reservedSlotsByFacility]
    );

    const reservedSlots = useMemo(() => {
        if (!selectedFacility) {
            return [];
        }

        return [...(reservedSlotsByFacility[selectedFacility.id] || [])]
            .filter(slot => slot.date === formState.reservationDate)
            .sort((left, right) => left.startMinutes - right.startMinutes);
    }, [formState.reservationDate, reservedSlotsByFacility, selectedFacility]);

    const startMinutes = useMemo(() => fromParts(formState.startTime), [formState.startTime]);
    const endMinutes = useMemo(() => fromParts(formState.endTime), [formState.endTime]);

    const conflictMessage = useMemo(() => {
        if (startMinutes == null || endMinutes == null || endMinutes <= startMinutes) {
            return "";
        }

        const conflict = reservedSlots.find(slot => isOverlap(startMinutes, endMinutes, slot.startMinutes, slot.endMinutes));
        return conflict ? "This time slot is already reserved. Please choose a different time." : "";
    }, [endMinutes, reservedSlots, startMinutes]);

    useEffect(() => {
        if (!modalFacilityId) {
            return undefined;
        }
        const originalOverflow = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        return () => {
            document.body.style.overflow = originalOverflow;
        };
    }, [modalFacilityId]);

    const goBack = () => {
        if (typeof window.loadContent === "function" && config.isEmbedded && config.dashboardUrl) {
            window.loadContent(config.dashboardUrl);
            return;
        }
        if (config.dashboardUrl) {
            window.location.href = config.dashboardUrl;
        }
    };

    const refreshFacilitySlots = async (facilityId) => {
        if (!config.reservedSlotsUrl || !facilityId) {
            return;
        }

        setLoadingReservedTimes(current => ({ ...current, [facilityId]: true }));

        try {
            const url = `${config.reservedSlotsUrl}?facilityId=${encodeURIComponent(facilityId)}`;
            const response = await fetch(url, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            const result = await response.json();
            if (!response.ok || !result?.success || !Array.isArray(result.slots)) {
                return;
            }

            setReservedSlotsByFacility(current => ({
                ...current,
                [facilityId]: result.slots.map(normalizeSlot)
            }));
        } catch (error) {
            console.error(error);
        } finally {
            setLoadingReservedTimes(current => ({ ...current, [facilityId]: false }));
        }
    };

    const openModal = facilityId => {
        setModalFacilityId(facilityId);
        setStep(1);
        setErrors({});
        setFormState({
            reservationDate: initialDate,
            startTime: { hour: "", minute: "", period: "AM" },
            endTime: { hour: "", minute: "", period: "AM" },
            purpose: ""
        });
    };

    const closeModal = () => {
        setModalFacilityId(null);
        setIsSubmitting(false);
        setErrors({});
    };

    const validateStepOne = () => {
        const nextErrors = {};
        if (!formState.reservationDate) {
            nextErrors.reservationDate = "Select a reservation date.";
        }
        if (startMinutes == null) {
            nextErrors.startTime = "Select a valid start time.";
        }
        if (endMinutes == null) {
            nextErrors.endTime = "Select a valid end time.";
        }
        if (startMinutes != null && endMinutes != null && endMinutes <= startMinutes) {
            nextErrors.endTime = "End time must be later than start time.";
        }
        if (conflictMessage) {
            nextErrors.conflict = conflictMessage;
        }
        setErrors(nextErrors);
        return Object.keys(nextErrors).length === 0;
    };

    const validateStepTwo = () => {
        const nextErrors = {};
        if (!formState.purpose.trim()) {
            nextErrors.purpose = "Add a purpose or note for this reservation.";
        }
        if (conflictMessage) {
            nextErrors.conflict = conflictMessage;
        }
        setErrors(nextErrors);
        return Object.keys(nextErrors).length === 0;
    };

    const submitReservation = async () => {
        if (!selectedFacility || !validateStepTwo()) {
            return;
        }

        setIsSubmitting(true);
        setBanner(null);

        try {
            const payload = new URLSearchParams();
            payload.append("FacilityId", String(selectedFacility.id));
            payload.append("ReservationDate", formState.reservationDate);
            payload.append("StartTime", toTimeSpan(startMinutes));
            payload.append("EndTime", toTimeSpan(endMinutes));
            payload.append("Purpose", formState.purpose.trim());

            const response = await fetch(config.reserveUrl, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin",
                body: payload
            });

            const result = await response.json();
            if (!response.ok || !result || result.success === false) {
                setErrors(current => ({ ...current, conflict: result?.message || "Unable to complete the reservation." }));
                setIsSubmitting(false);
                return;
            }

            const savedReservation = result?.reservation || {
                facilityId: selectedFacility.id,
                date: formState.reservationDate,
                start: toTimeSpan(startMinutes).slice(0, 5),
                end: toTimeSpan(endMinutes).slice(0, 5)
            };

            const savedSlot = normalizeSlot({
                date: savedReservation.date,
                start: savedReservation.start,
                end: savedReservation.end
            });

            setReservedSlotsByFacility(current => {
                const existing = current[savedReservation.facilityId] || [];
                const alreadyExists = existing.some(slot =>
                    slot.date === savedSlot.date &&
                    slot.start === savedSlot.start &&
                    slot.end === savedSlot.end);

                if (alreadyExists) {
                    return current;
                }

                return {
                    ...current,
                    [savedReservation.facilityId]: [...existing, savedSlot]
                        .sort((left, right) => {
                            if (left.date === right.date) {
                                return left.startMinutes - right.startMinutes;
                            }

                            return left.date.localeCompare(right.date);
                        })
                };
            });

            setShowReservedTimes(current => ({
                ...current,
                [savedReservation.facilityId]: true
            }));
            await refreshFacilitySlots(savedReservation.facilityId);
            setActivityCount(current => current + 1);
            if (window.homeownerDashboardShell?.invalidate) {
                window.homeownerDashboardShell.invalidate(config.sectionUrl);
                window.homeownerDashboardShell.invalidate(config.dashboardUrl);
                window.homeownerDashboardShell.invalidate(config.historyUrl);
            }
            setBanner({ type: "success", message: result.message || "Facility reserved successfully." });
            closeModal();
        } catch (error) {
            console.error(error);
            setErrors(current => ({ ...current, conflict: "Unable to complete the reservation." }));
        } finally {
            setIsSubmitting(false);
        }
    };

    const modal = selectedFacility ? html`
        <div className="reservation-react-modal-backdrop" onClick=${closeModal}>
            <div className="reservation-react-modal" onClick=${event => event.stopPropagation()}>
                <div className="reservation-react-modal-header">
                    <div>
                        <h3>Reserve ${selectedFacility.name}</h3>
                        <p className="reservation-react-time-help">Complete the schedule first, then confirm your purpose and booking summary.</p>
                    </div>
                    <button type="button" className="reservation-react-close" onClick=${closeModal} aria-label="Close reservation modal">
                        <i className="fas fa-times"></i>
                    </button>
                </div>
                <div className="reservation-react-modal-body">
                    <div className="reservation-react-stepper">
                        <div className=${`reservation-react-step ${step === 1 ? "active" : ""}`}>
                            <span>Step 1</span>
                            <strong>Pick date and time</strong>
                        </div>
                        <div className=${`reservation-react-step ${step === 2 ? "active" : ""}`}>
                            <span>Step 2</span>
                            <strong>Confirm reservation</strong>
                        </div>
                    </div>
                    ${step === 1 ? html`
                        <div className="reservation-react-facility-summary">
                            <img src=${selectedFacility.imageUrl} alt=${selectedFacility.name} />
                            <div className="reservation-react-summary-card">
                                <h4>${selectedFacility.name}</h4>
                                <p>${selectedFacility.description || "Shared amenity reservation for residents."}</p>
                                <div className="reservation-react-meta">
                                    <span className="reservation-react-pill capacity"><i className="fas fa-users"></i> Capacity ${selectedFacility.capacity ?? "N/A"}</span>
                                    <span className="reservation-react-rating"><i className="fas fa-star"></i> ${formatRatingLabel(selectedFacility.ratingValue)}</span>
                                </div>
                                <div className="reservation-react-field">
                                    <label>Reservation date</label>
                                    <input type="date" value=${formState.reservationDate} min=${new Date().toISOString().slice(0, 10)} onChange=${event => {
                                        setFormState(current => ({ ...current, reservationDate: event.target.value }));
                                        setErrors({});
                                    }} />
                                    ${errors.reservationDate ? html`<div className="reservation-react-field-error">${errors.reservationDate}</div>` : null}
                                </div>
                            </div>
                        </div>
                        <div className="reservation-react-field-grid">
                            <div>
                                <${TimePicker}
                                    label="Start time"
                                    value=${formState.startTime}
                                    bookedSlots=${reservedSlots}
                                    mode="start"
                                    onChange=${nextValue => {
                                        setFormState(current => ({ ...current, startTime: nextValue }));
                                        setErrors({});
                                    }} />
                                ${errors.startTime ? html`<div className="reservation-react-field-error">${errors.startTime}</div>` : null}
                            </div>
                            <div>
                                <${TimePicker}
                                    label="End time"
                                    value=${formState.endTime}
                                    bookedSlots=${reservedSlots}
                                    otherMinutes=${startMinutes}
                                    mode="end"
                                    onChange=${nextValue => {
                                        setFormState(current => ({ ...current, endTime: nextValue }));
                                        setErrors({});
                                    }} />
                                ${errors.endTime ? html`<div className="reservation-react-field-error">${errors.endTime}</div>` : null}
                            </div>
                        </div>
                        <div className="reservation-react-duration">
                            <strong>Duration:</strong> ${durationLabel(startMinutes, endMinutes) || "Select start and end times"}
                        </div>
                        <div>
                            <strong className="reservation-react-time-help">Reserved time slots on ${formatShortDate(formState.reservationDate)}</strong>
                            <div className="reservation-react-slot-strip">
                                ${reservedSlots.length ? reservedSlots.map(slot => html`
                                    <span className="reservation-react-slot-chip booked" key=${`${slot.date}-${slot.start}-${slot.end}`}>
                                        ${formatMinutes(slot.startMinutes)} - ${formatMinutes(slot.endMinutes)}
                                    </span>
                                `) : html`<span className="reservation-react-slot-chip">No existing bookings on this date</span>`}
                            </div>
                        </div>
                        ${errors.conflict || conflictMessage ? html`<div className="reservation-react-warning">${errors.conflict || conflictMessage}</div>` : null}
                    ` : html`
                        <div className="reservation-react-confirm-card">
                            <h4>Reservation summary</h4>
                            <p>You are reserving <strong>${selectedFacility.name}</strong> on <strong>${formatDate(formState.reservationDate)}</strong>, <strong>${formatMinutes(startMinutes)} - ${formatMinutes(endMinutes)}</strong>.</p>
                            <p><strong>Duration:</strong> ${durationLabel(startMinutes, endMinutes)}</p>
                        </div>
                        <div className="reservation-react-field">
                            <label>Purpose / notes</label>
                            <textarea value=${formState.purpose} placeholder="Tell the admin what this reservation is for." onChange=${event => {
                                setFormState(current => ({ ...current, purpose: event.target.value }));
                                setErrors({});
                            }}></textarea>
                            ${errors.purpose ? html`<div className="reservation-react-field-error">${errors.purpose}</div>` : null}
                        </div>
                        ${errors.conflict || conflictMessage ? html`<div className="reservation-react-warning">${errors.conflict || conflictMessage}</div>` : null}
                    `}
                </div>
                <div className="reservation-react-modal-footer">
                    <button type="button" className="reservation-react-secondary" onClick=${step === 1 ? closeModal : () => setStep(1)}>
                        <i className="fas fa-arrow-left"></i>
                        ${step === 1 ? "Cancel" : "Back"}
                    </button>
                    ${step === 1 ? html`
                        <button type="button" className="reservation-react-primary" onClick=${() => validateStepOne() && setStep(2)}>
                            Continue
                            <i className="fas fa-arrow-right"></i>
                        </button>
                    ` : html`
                        <button type="button" className="reservation-react-primary" disabled=${isSubmitting} onClick=${submitReservation}>
                            <i className="fas fa-check"></i>
                            ${isSubmitting ? "Confirming..." : "Confirm reservation"}
                        </button>
                    `}
                </div>
            </div>
        </div>
    ` : null;

    return html`
        <div className="reservation-react-shell">
            <div className="reservation-react-header">
                <div className="reservation-react-title">
                    <h2>Available Facilities</h2>
                    <p>Reserve subdivision amenities from a cleaner booking flow with clear slot visibility, conflict detection, and a guided confirmation step.</p>
                </div>
                <button type="button" className="reservation-react-back" onClick=${goBack}>
                    <i className="fas fa-arrow-left"></i>
                    Back to Dashboard
                </button>
            </div>
            <div className="reservation-react-summary">
                <div className="reservation-react-stat">
                    <span>Confirmed bookings</span>
                    <strong>${activityCount}</strong>
                </div>
                <div className="reservation-react-stat">
                    <span>Facilities ready</span>
                    <strong>${facilities.length}</strong>
                </div>
                <div className="reservation-react-stat">
                    <span>Reserved slots</span>
                    <strong>${totalReservedSlots}</strong>
                </div>
            </div>
            ${banner ? html`
                <div className=${`reservation-react-banner ${banner.type}`}>
                    <i className=${banner.type === "success" ? "fas fa-check-circle" : "fas fa-exclamation-circle"}></i>
                    <span>${banner.message}</span>
                </div>
            ` : null}
            <div className="reservation-react-grid">
                ${facilities.map(facility => {
                    const slots = [...(reservedSlotsByFacility[facility.id] || [])];
                    const totalBookedMinutes = slots.reduce((total, slot) => total + (slot.endMinutes - slot.startMinutes), 0);
                    const availability = getSlotState(totalBookedMinutes);
                    const expanded = showReservedTimes[facility.id];
                    return html`
                        <article className="reservation-react-card" key=${facility.id}>
                            <img className="reservation-react-card-image" src=${facility.imageUrl} alt=${facility.name} loading="lazy" decoding="async" />
                            <div className="reservation-react-card-body">
                                <div className="reservation-react-card-top">
                                    <h3>${facility.name}</h3>
                                    <span className=${`reservation-react-pill ${availability.className}`}>${availability.label}</span>
                                </div>
                                <p className="reservation-react-card-description">${facility.description || "Shared resident amenity available for subdivision bookings."}</p>
                                <div className="reservation-react-meta">
                                    <span className="reservation-react-pill capacity"><i className="fas fa-users"></i> Capacity ${facility.capacity ?? "N/A"}</span>
                                    <span className="reservation-react-rating"><i className="fas fa-star"></i> ${formatRatingLabel(facility.ratingValue)}</span>
                                </div>
                                <div className="reservation-react-card-actions">
                                    <button
                                        type="button"
                                        className="reservation-react-link"
                                        onClick=${async () => {
                                            const nextExpanded = !showReservedTimes[facility.id];
                                            setShowReservedTimes(current => ({ ...current, [facility.id]: nextExpanded }));
                                            if (nextExpanded) {
                                                await refreshFacilitySlots(facility.id);
                                            }
                                        }}>
                                        ${expanded ? "Hide reserved times" : "View reserved times"}
                                    </button>
                                    <button type="button" className="reservation-react-primary" onClick=${() => openModal(facility.id)}>
                                        <i className="fas fa-calendar-plus"></i>
                                        Reserve
                                    </button>
                                </div>
                                ${expanded ? html`
                                    <div className="reservation-react-reserved-list">
                                        ${loadingReservedTimes[facility.id]
                                            ? html`<div className="reservation-react-reserved-item">Loading reserved times...</div>`
                                            : slots.length ? slots.map(slot => html`
                                            <div className="reservation-react-reserved-item" key=${`${facility.id}-${slot.date}-${slot.start}-${slot.end}`}>
                                                ${formatShortDate(slot.date)} · ${formatMinutes(slot.startMinutes)} - ${formatMinutes(slot.endMinutes)} · Booked
                                            </div>
                                        `) : html`<div className="reservation-react-reserved-item">No booked slots yet.</div>`}
                                    </div>
                                ` : null}
                            </div>
                        </article>
                    `;
                })}
            </div>
            ${modal}
        </div>
    `;
}

export function mountReservationApp(rootElement) {
    const config = parseConfig(rootElement);
    const root = createRoot(rootElement);
    root.render(html`<${ReservationApp} config=${config} />`);
    window.__reservationAppCleanup = () => root.unmount();
    return window.__reservationAppCleanup;
}
