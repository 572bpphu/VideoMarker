// ═══════════════════════════════════════════════════════════════════
//  timeline.js  –  Video Timeline Marker (ASP.NET MVC edition)
//  Reads window.VIDEO_ID, window.INIT_SEGMENTS, window.VIDEO_DURATION
//  Communicates with /api/segments/* REST endpoints
// ═══════════════════════════════════════════════════════════════════

(function () {
    'use strict';

    // ── Constants ──────────────────────────────────────────────────────
    var PRESET_NORMAL_COLORS = [
        '#38bdf8', '#3b82f6', '#4ade80', '#10b981',
        '#a78bfa', '#818cf8', '#facc15', '#fb923c',
        '#e879f9', '#2dd4bf', '#ec4899', '#f43f5e'
    ];
    var INCIDENT_COLOR = '#ef4444';

    // ── State ──────────────────────────────────────────────────────────
    var duration = window.VIDEO_DURATION || 0;
    var initialSegments = [];   // baseline from DB
    var segments = [];          // in-memory working draft
    var isDirty = false;
    var DRAFT_STORAGE_KEY = 'video_timeline_draft_' + (window.VIDEO_ID || 0);
    var selectedNormalColor = '#38bdf8';
    try {
        var savedColor = localStorage.getItem('video_timeline_normal_color');
        if (savedColor) selectedNormalColor = savedColor;
    } catch (e) { }
    var pendingStart = null;
    var pendingEnd = null;
    var segType = 'normal';
    var editingId = null;

    // ── Anti-Spam Protection ───────────────────────────────────────────
    var isSavingSegment = false;
    var lastMarkActionTime = 0;
    var MARK_COOLDOWN_MS = 150; // Khoảng đệm 150ms chống nhấp đúp (double-click) vô ý

    function setDirty(val) {
        isDirty = !!val;
        if (isDirty) {
            try {
                localStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(segments));
            } catch (e) { }
        }
        updateDirtyUI();
    }

    function updateDirtyUI() {
        var saveBtn = G('btn-save-db');
        var discardBtn = G('btn-discard-db');
        var badge = G('db-status-badge');

        if (saveBtn) {
            saveBtn.classList.toggle('dirty', isDirty);
            saveBtn.textContent = isDirty ? 'Lưu vào CSDL (' + segments.length + ')' : 'Lưu vào CSDL';
        }
        if (discardBtn) {
            discardBtn.disabled = !isDirty;
        }
        if (badge) {
            badge.className = 'db-status-badge ' + (isDirty ? 'dirty' : 'synced');
            badge.textContent = isDirty ? 'Có thay đổi chưa lưu vào CSDL' : 'Đã đồng bộ với CSDL';
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────
    function G(id) { return document.getElementById(id); }

    function fmt(sec) {
        if (sec == null || !isFinite(sec)) return '--:--';
        var h = Math.floor(sec / 3600);
        var m = Math.floor((sec % 3600) / 60);
        var s = Math.floor(sec % 60);
        var mm = ('0' + m).slice(-2);
        var ss = ('0' + s).slice(-2);
        return h > 0 ? (h + ':' + mm + ':' + ss) : (mm + ':' + ss);
    }
    // hàm số thập phân
    function fmtDurationMs(seconds) {
        seconds = Number(seconds) || 0;

        var minutes = Math.floor(seconds / 60);
        var secs = Math.floor(seconds % 60);
        var millis = Math.round((seconds - Math.floor(seconds)) * 100);

        if (millis === 100) {
            millis = 0;
            secs++;

            if (secs === 60) {
                secs = 0;
                minutes++;
            }
        }

        return String(minutes).padStart(2, '0') + ':' +
            String(secs).padStart(2, '0') + '.' +
            String(millis).padStart(2, '0');
    }


    function getDuration() {
        if (duration > 0) return duration;
        if (videoEl && isFinite(videoEl.duration) && videoEl.duration > 0) {
            duration = videoEl.duration;
            if (durDisp) durDisp.textContent = fmtDurationMs(duration);
        }
        return duration || 0;
    }

    function toPct(t) {
        var d = getDuration();
        if (!d || d <= 0) return 0;
        return (t / d) * 100;
    }

    function pxToTime(px) {
        var d = getDuration();
        var w = G('timeline-wrap').getBoundingClientRect().width;
        return Math.max(0, Math.min(d, (px / w) * d));
    }

    var toastTimer;
    function showToast(msg, ok) {
        var el = G('toast');
        el.textContent = msg;
        el.className = 'show' + (ok ? ' ok' : '');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { el.className = ''; }, 2700);
    }

    function hasOverlap(start, end, excludeId, targetType) {
        var checkType = targetType || segType;
        return segments.some(function (s) {
            if (excludeId != null && s.id === excludeId) return false;
            // Chỉ cấm đè mốc nếu CÙNG LOẠI thao tác (Đúng đè Đúng, Sai đè Sai)
            if (s.type !== checkType) return false;
            return start < s.endTime && end > s.startTime;
        });
    }

    function setSaving(on) {
        G('saving-indicator').style.display = on ? 'block' : 'none';
    }

    var activeFilter = 'all'; // 'all' | 'normal' | 'incident'

    function parseTime(input) {
        if (input == null) return null;
        var str = String(input).trim();
        if (!str) return null;
        if (str.indexOf(':') !== -1) {
            var parts = str.split(':').map(Number);
            if (parts.some(isNaN)) return null;
            if (parts.length === 2) return parts[0] * 60 + parts[1];
            if (parts.length === 3) return parts[0] * 3600 + parts[1] * 60 + parts[2];
        }
        var num = parseFloat(str);
        return isNaN(num) ? null : num;
    }

    // ── API helpers ───────────────────────────────────────────────────
    var VIDEO_ID = window.VIDEO_ID;

    function apiCall(method, url, body) {
        var opts = {
            method: method,
            headers: { 'Content-Type': 'application/json' }
        };
        if (body !== undefined) opts.body = JSON.stringify(body);
        return fetch(url, opts);
    }

    function apiAddSegment(data) {
        return apiCall('POST', '/api/segments', data);
    }
    function apiDeleteSegment(id) {
        return apiCall('DELETE', '/api/segments/' + id);
    }
    function apiUpdateSegment(id, data) {
        return apiCall('PUT', '/api/segments/' + id, data);
    }
    function apiUpdateLabel(id, label) {
        return apiUpdateSegment(id, { label: label });
    }
    function apiUpdateDuration(dur) {
        return apiCall('PUT', '/api/segments/video/' + VIDEO_ID + '/duration', { duration: dur });
    }
    function apiSaveBatch(videoId, segs) {
        var payload = {
            Segments: segs.map(function (s) {
                return {
                    VideoSessionId: videoId,
                    Label: s.label || '',
                    StartTime: s.startTime,
                    EndTime: s.endTime,
                    Type: s.type || 'normal',
                    Color: s.color || (s.type === 'incident' ? INCIDENT_COLOR : '#38bdf8')
                };
            })
        };
        return apiCall('POST', '/api/segments/video/' + videoId + '/save-batch', payload);
    }

    // ── DOM refs ──────────────────────────────────────────────────────
    var videoEl = G('video');
    var tlWrap = G('timeline-wrap');
    var playheadEl = G('playhead');
    var pendingInd = G('pending-ind');
    var ticksEl = G('ticks');
    var ctDisp = G('ct-disp');
    var durDisp = G('dur-disp');
    var secStats = G('sec-stats');
    var tbodyEl = G('seg-table-body');
    var selS = G('sel-s');
    var selE = G('sel-e');
    var selD = G('sel-d');
    var overlapW = G('overlap-warn');
    var labelInput = G('label-input');
    var tooltip = G('tooltip');
    var segPopup = G('seg-annotation-popup');
    var editModal = G('edit-modal');
    var editInput = G('edit-input');

    // ── Color Palette Management ────────────────────────────────────
    var COLOR_HISTORY_KEY = 'video_timeline_color_history';
    var MAX_COLOR_HISTORY = 8;

    function loadColorHistory() {
        try {
            var raw = localStorage.getItem(COLOR_HISTORY_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch (e) { return []; }
    }

    function saveColorHistory(color) {
        var history = loadColorHistory();
        // Xóa màu cũ nếu đã tồn tại để đưa lên đầu
        history = history.filter(function (c) { return c.toLowerCase() !== color.toLowerCase(); });
        history.unshift(color);
        if (history.length > MAX_COLOR_HISTORY) history = history.slice(0, MAX_COLOR_HISTORY);
        try { localStorage.setItem(COLOR_HISTORY_KEY, JSON.stringify(history)); } catch (e) { }
        return history;
    }

    function removeColorFromHistory(colorToRemove, currentColor, updateColorUIFn) {
        var history = loadColorHistory();
        history = history.filter(function (c) {
            return c.toLowerCase() !== colorToRemove.toLowerCase();
        });
        try { localStorage.setItem(COLOR_HISTORY_KEY, JSON.stringify(history)); } catch (e) { }
        renderColorHistory(currentColor, updateColorUIFn);
    }

    function clearAllColorHistory(currentColor, updateColorUIFn) {
        var history = loadColorHistory();
        if (history.length === 0) return;
        confirmAsync(
            'Bạn có chắc chắn muốn xóa toàn bộ lịch sử màu gần đây không?',
            'Yêu cầu xác nhận',
            'Xóa tất cả',
            'Hủy'
        ).then(function (ok) {
            if (!ok) return;
            try { localStorage.removeItem(COLOR_HISTORY_KEY); } catch (e) { }
            renderColorHistory(currentColor, updateColorUIFn);
        });
    }

    function renderColorHistory(currentColor, updateColorUIFn) {
        var historyWrap = G('color-history-wrap');
        var historySwatches = G('color-history-swatches');
        var clearBtn = G('btn-clear-color-history');
        if (!historyWrap || !historySwatches) return;

        historyWrap.style.display = 'flex';
        historySwatches.innerHTML = '';

        var history = loadColorHistory();

        if (clearBtn) {
            clearBtn.style.display = history.length > 0 ? 'inline-block' : 'none';
            clearBtn.onclick = function () {
                clearAllColorHistory(currentColor, updateColorUIFn);
            };
        }

        if (history.length === 0) {
            return;
        }

        history.forEach(function (c) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'color-history-swatch' + (c.toLowerCase() === (currentColor || '').toLowerCase() ? ' active' : '');
            btn.style.background = c;
            btn.title = c.toUpperCase() + ' (Click để chọn | Click chuột phải hoặc bấm × để xóa)';

            var delSpan = document.createElement('span');
            delSpan.className = 'color-history-remove';
            delSpan.innerHTML = '&times;';
            delSpan.title = 'Xóa màu ' + c.toUpperCase();
            delSpan.addEventListener('click', function (e) {
                e.stopPropagation();
                e.preventDefault();
                removeColorFromHistory(c, currentColor, updateColorUIFn);
            });
            btn.appendChild(delSpan);

            btn.addEventListener('click', function () {
                updateColorUIFn(c);
                if (segType !== 'normal') setType('normal');
            });

            btn.addEventListener('contextmenu', function (e) {
                e.preventDefault();
                removeColorFromHistory(c, currentColor, updateColorUIFn);
            });

            historySwatches.appendChild(btn);
        });
    }

    function initColorPalette() {
        var swatchesEl = G('color-swatches');
        var hexDisp = G('current-color-hex');
        var customInp = G('input-custom-color');
        var customPrev = G('custom-color-preview');
        if (!swatchesEl) return;

        function updateColorUI(c) {
            if (!c) return;
            selectedNormalColor = c;
            try { localStorage.setItem('video_timeline_normal_color', c); } catch (e) { }

            if (hexDisp) hexDisp.textContent = c.toUpperCase();
            if (customInp) customInp.value = c;
            if (customPrev) customPrev.style.background = c;

            // Đánh dấu swatch đang được chọn
            var swatches = swatchesEl.querySelectorAll('.color-swatch');
            swatches.forEach(function (sw) {
                var swColor = sw.getAttribute('data-color');
                if (swColor && swColor.toLowerCase() === c.toLowerCase()) {
                    sw.classList.add('active');
                } else {
                    sw.classList.remove('active');
                }
            });

            // Render lịch sử màu
            renderColorHistory(c, updateColorUIAndSaveHistory);
        }

        function updateColorUIAndSaveHistory(c) {
            if (!c) return;
            saveColorHistory(c);
            updateColorUI(c);
        }

        swatchesEl.innerHTML = '';
        PRESET_NORMAL_COLORS.forEach(function (c) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'color-swatch' + (c.toLowerCase() === selectedNormalColor.toLowerCase() ? ' active' : '');
            btn.style.background = c;
            btn.setAttribute('data-color', c);
            btn.title = 'Chọn màu ' + c;
            btn.addEventListener('click', function () {
                updateColorUIAndSaveHistory(c);
                if (segType !== 'normal') setType('normal');
            });
            swatchesEl.appendChild(btn);
        });

        if (customInp) {
            customInp.addEventListener('input', function (e) {
                updateColorUI(e.target.value);
                if (segType !== 'normal') setType('normal');
            });
            // Lưu vào lịch sử khi người dùng xác nhận chọn màu (đóng picker)
            customInp.addEventListener('change', function (e) {
                updateColorUIAndSaveHistory(e.target.value);
                if (segType !== 'normal') setType('normal');
            });
        }

        updateColorUI(selectedNormalColor);
        renderColorHistory(selectedNormalColor, updateColorUIAndSaveHistory);
    }


    // ── Bootstrap from server-seeded data ────────────────────────────
    function init() {
        initialSegments = (window.INIT_SEGMENTS || []).map(function (s) { return Object.assign({}, s); });
        segments = initialSegments.map(function (s) { return Object.assign({}, s); });

        initColorPalette();

        // Kiểm tra xem có bản nháp chưa lưu trước đó trong LocalStorage không
        try {
            var savedDraftStr = localStorage.getItem(DRAFT_STORAGE_KEY);
            if (savedDraftStr) {
                var draftSegs = JSON.parse(savedDraftStr);
                if (Array.isArray(draftSegs) && JSON.stringify(draftSegs) !== JSON.stringify(initialSegments)) {
                    confirmAsync(
                        'Hệ thống phát hiện có bản nháp chưa lưu trước đó của video này. Bạn có muốn khôi phục lại không?',
                        'Yêu cầu xác nhận',
                        'Khôi phục bản nháp',
                        'Bỏ qua bản nháp'
                    ).then(function (ok) {
                        if (ok) {
                            segments = draftSegs;
                            setDirty(true);
                            render();
                            showToast('Đã khôi phục bản nháp chưa lưu.', true);
                        } else {
                            localStorage.removeItem(DRAFT_STORAGE_KEY);
                            setDirty(false);
                        }
                    });
                } else {
                    localStorage.removeItem(DRAFT_STORAGE_KEY);
                }
            }
        } catch (e) { }

        if (!duration && videoEl && isFinite(videoEl.duration) && videoEl.duration > 0) {
            duration = videoEl.duration;
        }

        if (duration > 0) {
            durDisp.textContent = fmtDurationMs(duration);
            buildTicks();
        }

        // Setup filter tabs
        ['all', 'normal', 'incident'].forEach(function (f) {
            var btn = G('tab-filter-' + f);
            if (btn) {
                btn.addEventListener('click', function () {
                    activeFilter = f;
                    document.querySelectorAll('.tab-btn').forEach(function (b) { b.classList.remove('active'); });
                    btn.classList.add('active');
                    updateStats();
                });
            }
        });

        updateDirtyUI();
        render();
        updateSel();
    }

    // ── Build tick marks ─────────────────────────────────────────────
    function buildTicks() {
        ticksEl.innerHTML = '';
        var step = duration <= 30 ? 5
            : duration <= 90 ? 10
                : duration <= 300 ? 30
                    : duration <= 1800 ? 60 : 300;
        for (var t = 0; t <= duration; t += step) {
            var el = document.createElement('span');
            el.className = 'tick';
            el.style.left = toPct(t) + '%';
            el.textContent = fmtDurationMs(t);
            ticksEl.appendChild(el);
        }
    }

    // ── Render timeline segments ──────────────────────────────────────
    function render() {
        tlWrap.querySelectorAll('.seg-block,.pend-preview').forEach(function (el) { el.remove(); });

        segments.forEach(function (seg) {
            var isInc = seg.type === 'incident';
            var col = seg.color || (isInc ? INCIDENT_COLOR : '#38bdf8');
            var topPos = isInc ? '28px' : '2px';
            var heightVal = '24px';
            var segDur = Math.max(0, seg.endTime - seg.startTime);
            var wPct = toPct(segDur);
            var isShort = segDur < 1.5 || wPct < 1.5;

            var div = document.createElement('div');
            div.setAttribute('data-id', seg.id);
            div.className = 'seg-block ' + (isInc ? 'seg-incident' : 'seg-normal');
            div.style.cssText = [
                'position:absolute;top:' + topPos + ';height:' + heightVal + ';',
                'left:' + toPct(seg.startTime) + '%;',
                'width:' + wPct + '%;',
                'min-width:2px;box-sizing:border-box;',
                'background:' + col + (isInc ? '42;' : '35;'),
                'border-left:1px solid ' + col + ';',
                'border-right:1px solid ' + col + ';',
                'border-top:1px solid ' + col + '55;',
                'border-bottom:1px solid ' + col + '55;',
                'border-radius:2px;',
                isInc
                    ? 'background-image:repeating-linear-gradient(135deg,transparent,transparent 3px,' + col + '28 3px,' + col + '28 6px);'
                    : '',
                'z-index:3;cursor:pointer;overflow:hidden;',
                'display:flex;align-items:center;',
                isShort ? 'padding:0;' : 'padding:0 4px;'
            ].join('');

            // Chỉ hiển thị nhãn chữ nếu đoạn đủ rộng, tránh làm phình to đoạn ngắn
            if (!isShort && seg.label) {
                var lbl = document.createElement('span');
                lbl.style.cssText = 'font-size:.56rem;font-weight:700;color:' + col + ';white-space:nowrap;overflow:hidden;text-overflow:ellipsis;pointer-events:none;line-height:1;display:block;';
                lbl.textContent = seg.label;
                div.appendChild(lbl);
            }

            // Hover tương tác 2 chiều + Hiển thị pop-up chú thích
            div.addEventListener('mouseenter', function (e) {
                div.classList.add('seg-highlighted');

                // 1. Highlight dòng tương ứng trên bảng danh sách và cuộn tới nếu cần
                var targetTr = tbodyEl.querySelector('tr[data-id="' + seg.id + '"]');
                if (targetTr) {
                    targetTr.classList.add('tbl-row-highlighted');
                    targetTr.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }

                // 2. Ẩn tooltip chung để chỉ tập trung vào pop-up chú thích
                if (tooltip) tooltip.style.display = 'none';

                // 3. Hiển thị pop-up chú thích (chỉ hiển thị nội dung chú thích/nhãn)
                if (segPopup) {
                    segPopup.textContent = (seg.label && seg.label.trim()) ? seg.label : '(Chưa có chú thích)';
                    segPopup.style.borderColor = col;
                    segPopup.style.display = 'block';

                    var rect = div.getBoundingClientRect();
                    var popRect = segPopup.getBoundingClientRect();

                    var left = (rect.left + rect.width / 2) - (popRect.width / 2);
                    var minLeft = 10;
                    var maxLeft = window.innerWidth - popRect.width - 10;
                    left = Math.max(minLeft, Math.min(maxLeft, left));

                    var top = rect.top - popRect.height - 8;
                    if (top < 10) {
                        top = rect.bottom + 8;
                    }

                    segPopup.style.left = left + 'px';
                    segPopup.style.top = top + 'px';
                }
            });

            div.addEventListener('mousemove', function (e) {
                if (tooltip) tooltip.style.display = 'none';
                e.stopPropagation();
            });

            div.addEventListener('mouseleave', function () {
                div.classList.remove('seg-highlighted');

                // 1. Bỏ highlight dòng trên bảng
                var targetTr = tbodyEl.querySelector('tr[data-id="' + seg.id + '"]');
                if (targetTr) {
                    targetTr.classList.remove('tbl-row-highlighted');
                }

                // 2. Ẩn pop-up chú thích
                if (segPopup) {
                    segPopup.style.display = 'none';
                }
            });

            div.addEventListener('click', function (e) {
                e.stopPropagation();
                videoEl.currentTime = seg.startTime;
            });
            tlWrap.appendChild(div);
        });

        // Pending preview (vẽ theo đúng làn loại thao tác đang chọn)
        if (pendingStart !== null && pendingEnd !== null) {
            var ps = Math.min(pendingStart, pendingEnd);
            var pe = Math.max(pendingStart, pendingEnd);
            var isInc = segType === 'incident';
            var col = isInc ? INCIDENT_COLOR : '#22c55e';
            var topPos = isInc ? '28px' : '2px';
            var pre = document.createElement('div');
            pre.className = 'pend-preview';
            pre.style.cssText = 'position:absolute;top:' + topPos + ';height:24px;left:' + toPct(ps) + '%;width:' + toPct(pe - ps) + '%;min-width:2px;box-sizing:border-box;background:' + col + '1a;border:1px dashed ' + col + ';z-index:4;pointer-events:none;border-radius:2px;padding:0;';
            tlWrap.appendChild(pre);
        }

        // Pending start marker
        if (pendingStart !== null) {
            pendingInd.style.display = 'block';
            pendingInd.style.left = toPct(pendingStart) + '%';
            pendingInd.setAttribute('data-time', fmtDurationMs(pendingStart));
        } else {
            pendingInd.style.display = 'none';
        }

        updateStats();
    }

    // ── Update selection display ──────────────────────────────────────
    function updateSel() {
        var selS = G('sel-s');
        var selE = G('sel-e');
        var selD = G('sel-d');
        var overlapW = G('overlap-warn');

        if (selS) selS.textContent = pendingStart !== null ? fmtDurationMs(pendingStart) : '--:--';
        if (selE) selE.textContent = pendingEnd !== null ? fmtDurationMs(pendingEnd) : '--:--';

        if (pendingStart !== null && pendingEnd !== null) {
            if (selD) selD.textContent = fmtDurationMs(Math.abs(pendingEnd - pendingStart));
            var ps = Math.min(pendingStart, pendingEnd);
            var pe = Math.max(pendingStart, pendingEnd);
            overlapW.style.display = hasOverlap(ps, pe, null, segType) ? 'block' : 'none';
            if (overlapW) overlapW.style.display = hasOverlap(ps, pe, null, segType) ? 'block' : 'none';
        } else {
            if (selD) selD.textContent = '--:--';
            if (overlapW) overlapW.style.display = 'none';
        }

        var btnStart = G('btn-set-start');
        var btnEnd = G('btn-set-end');

        if (btnStart) btnStart.classList.toggle('lit', pendingStart !== null);
        if (btnEnd) btnEnd.classList.toggle('lit', pendingEnd !== null && pendingStart !== null);
    }

    // ── Stats + Interactive Segment Table ──────────────────────────────
    function updateStats() {
        var countNormal = segments.filter(function (s) { return s.type !== 'incident'; }).length;
        var countIncident = segments.filter(function (s) { return s.type === 'incident'; }).length;
        var totalCount = segments.length;

        // Phương án B: Tổng thời gian Đúng đã khấu trừ các khoảng Sai lồng bên trong
        var normals = segments.filter(function (s) { return s.type !== 'incident'; });
        var incidents = segments.filter(function (s) { return s.type === 'incident'; });

        var totalNetCorrectSec = 0;
        normals.forEach(function (norm) {
            var normDur = Math.max(0, norm.endTime - norm.startTime);
            var overlapDur = 0;
            incidents.forEach(function (inc) {
                var oStart = Math.max(norm.startTime, inc.startTime);
                var oEnd = Math.min(norm.endTime, inc.endTime);
                if (oEnd > oStart) {
                    overlapDur += (oEnd - oStart);
                }
            });
            totalNetCorrectSec += Math.max(0, normDur - overlapDur);
        });


        var totalIncidentSec = incidents.reduce(function (a, s) {
            return a + Math.max(0, s.endTime - s.startTime);
        }, 0);


        if (G('st-count')) G('st-count').textContent = totalCount;
        if (G('st-normal')) G('st-normal').textContent = countNormal;
        if (G('st-inc')) G('st-inc').textContent = countIncident;
        if (G('st-total')) G('st-total').textContent = fmtDurationMs(totalNetCorrectSec);
        if (G('st-inc-total')) G('st-inc-total').textContent = fmtDurationMs(totalIncidentSec);
        if (G('st-pct')) {
            var pct = duration > 0 ? ((totalNetCorrectSec / duration) * 100).toFixed(1) : '0.0';
            G('st-pct').textContent = pct + '%';
        }

        // Tab count badges
        if (G('cnt-all')) G('cnt-all').textContent = totalCount;
        if (G('cnt-normal')) G('cnt-normal').textContent = countNormal;
        if (G('cnt-incident')) G('cnt-incident').textContent = countIncident;

        secStats.classList.add('visible');

        // Filtered list
        var filtered = segments.filter(function (s) {
            if (activeFilter === 'normal') return s.type !== 'incident';
            if (activeFilter === 'incident') return s.type === 'incident';
            return true;
        });

        tbodyEl.innerHTML = '';
        if (filtered.length === 0) {
            tbodyEl.innerHTML = '<tr><td colspan="7" style="text-align:center;color:var(--muted);padding:20px;">Không có đoạn nào khớp với bộ lọc.</td></tr>';
        } else {
            filtered.forEach(function (seg, idx) {
                var isInc = seg.type === 'incident';
                var tr = document.createElement('tr');
                tr.setAttribute('data-id', seg.id);
                tr.style.borderLeft = '3px solid ' + (seg.color || (isInc ? '#ef4444' : '#38bdf8'));

                // Hover hàng bảng -> Highlight đoạn tương ứng trên timeline
                tr.addEventListener('mouseenter', function () {
                    var block = tlWrap.querySelector('.seg-block[data-id="' + seg.id + '"]');
                    if (block) {
                        block.classList.add('seg-highlighted');
                    }
                });
                tr.addEventListener('mouseleave', function () {
                    var block = tlWrap.querySelector('.seg-block[data-id="' + seg.id + '"]');
                    if (block) {
                        block.classList.remove('seg-highlighted');
                    }
                });

                var typeColHtml = isInc
                    ? '<span class="seg-badge badge-i">Sai</span>'
                    : '<div class="seg-badge-color-wrap">' +
                    '<input type="color" class="tbl-color-picker" data-id="' + seg.id + '" value="' + (seg.color || '#38bdf8') + '" title="Bấm để đổi màu thao tác này" />' +
                    '<span class="seg-badge badge-n" style="border-color:' + (seg.color || '#38bdf8') + ';color:' + (seg.color || '#38bdf8') + ';">Đúng</span>' +
                    '</div>';

                tr.innerHTML =
                    '<td style="text-align:center;font-weight:700;color:var(--muted);">' + (idx + 1) + '</td>' +
                    '<td>' + typeColHtml + '</td>' +
                    '<td><input type="text" class="tbl-input tbl-input-label" data-id="' + seg.id + '" value="' + (seg.label || '').replace(/"/g, '&quot;') + '" placeholder="Nhập tên thao tác..." /></td>' +
                    '<td><input type="text" class="tbl-input tbl-input-time tbl-input-start" data-id="' + seg.id + '" value="' + fmtDurationMs(seg.startTime) + '" title="Nhập mm:ss hoặc giây" /></td>' +
                    '<td><input type="text" class="tbl-input tbl-input-time tbl-input-end" data-id="' + seg.id + '" value="' + fmtDurationMs(seg.endTime) + '" title="Nhập mm:ss hoặc giây" /></td>' +
                    '<td style="text-align:right;font-weight:700;font-variant-numeric:tabular-nums;">' + fmtDurationMs(seg.endTime - seg.startTime) + '</td>' +
                    '<td>' +
                    '<div class="tbl-actions">' +
                    '<button class="tbl-btn tbl-btn-play" data-start="' + seg.startTime + '" title="Phát thử đoạn này">▶</button>' +
                    '<button class="tbl-btn tbl-btn-del"  data-id="' + seg.id + '" title="Xóa thao tác">🗑️</button>' +
                    '</div>' +
                    '</td>';

                tbodyEl.appendChild(tr);
            });
        }

        // Bind Play buttons
        tbodyEl.querySelectorAll('.tbl-btn-play').forEach(function (btn) {
            btn.addEventListener('click', function () {
                videoEl.currentTime = +btn.getAttribute('data-start');
                videoEl.play();
            });
        });

        // Bind Delete buttons
        tbodyEl.querySelectorAll('.tbl-btn-del').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var id = btn.getAttribute('data-id');
                var seg = segments.find(function (s) { return String(s.id) === String(id); });
                var desc = seg
                    ? ((seg.type === 'incident' ? 'thao tác Sai' : 'thao tác Đúng') + (seg.label ? ' "' + seg.label + '"' : '') + ' (' + fmtDurationMs(seg.startTime) + ' → ' + fmtDurationMs(seg.endTime) + ')')
                    : 'thao tác này';
                confirmAsync('Bạn có chắc chắn muốn xóa ' + desc + ' không?', 'Yêu cầu xác nhận', 'Xóa thao tác', 'Hủy').then(function (ok) {
                    if (ok) deleteSegment(id);
                });
            });
        });

        // Bind Color Pickers in Table (Lưu tạm bộ nhớ)
        tbodyEl.querySelectorAll('.tbl-color-picker').forEach(function (inp) {
            inp.addEventListener('change', function () {
                var id = inp.getAttribute('data-id');
                var seg = segments.find(function (s) { return String(s.id) === String(id); });
                if (!seg) return;
                seg.color = inp.value;
                setDirty(true);
                render();
                showToast('🎨 Đã đổi màu thao tác.', true);
            });
        });

        // Bind Label input edits (Lưu tạm bộ nhớ)
        tbodyEl.querySelectorAll('.tbl-input-label').forEach(function (inp) {
            inp.addEventListener('change', function () {
                var id = inp.getAttribute('data-id');
                var seg = segments.find(function (s) { return String(s.id) === String(id); });
                if (!seg) return;
                seg.label = inp.value.trim();
                setDirty(true);
                render();
                showToast('✏️ Đã cập nhật tên công đoạn.', true);
            });
        });

        // Helper to update segment times with overlap validation (Lưu tạm bộ nhớ)
        function handleTimeChange(id, startInputVal, endInputVal, isStartChanged) {
            var seg = segments.find(function (s) { return String(s.id) === String(id); });
            if (!seg) return;

            var newStart = parseTime(startInputVal);
            var newEnd = parseTime(endInputVal);

            if (newStart === null || newEnd === null || newStart < 0 || newEnd <= newStart || (duration > 0 && newEnd > duration)) {
                showToast(' Thời gian không hợp lệ (Bắt đầu phải nhỏ hơn Kết thúc và trong độ dài video).');
                render();
                return;
            }

            if (hasOverlap(newStart, newEnd, seg.id, seg.type)) {
                showToast(' Khoảng thời gian mới bị đè lên mốc cùng loại khác!');
                render();
                return;
            }

            seg.startTime = newStart;
            seg.endTime = newEnd;
            segments.sort(function (a, b) { return a.startTime - b.startTime; });
            setDirty(true);
            render();
            showToast(' Đã cập nhật mốc thời gian: ' + fmtDurationMs(seg.startTime) + ' ➔ ' + fmtDurationMs(seg.endTime), true);
        }

        // Bind Start & End input edits
        tbodyEl.querySelectorAll('.tbl-input-start').forEach(function (inp) {
            inp.addEventListener('change', function () {
                var id = inp.getAttribute('data-id');
                var tr = inp.closest('tr');
                var endVal = tr.querySelector('.tbl-input-end').value;
                handleTimeChange(id, inp.value, endVal, true);
            });
        });
        tbodyEl.querySelectorAll('.tbl-input-end').forEach(function (inp) {
            inp.addEventListener('change', function () {
                var id = inp.getAttribute('data-id');
                var tr = inp.closest('tr');
                var startVal = tr.querySelector('.tbl-input-start').value;
                handleTimeChange(id, startVal, inp.value, false);
            });
        });
    }

    // ── Set Start ─────────────────────────────────────────────────────
    G('btn-set-start').addEventListener('click', function () {
        if (!duration || isSavingSegment) return;
        var now = Date.now();
        if (now - lastMarkActionTime < MARK_COOLDOWN_MS) return;
        lastMarkActionTime = now;

        pendingStart = videoEl.currentTime;
        pendingEnd = null;
        overlapW.style.display = 'none';
        updateSel();
        render();
        showToast(' Đã đặt điểm bắt đầu: ' + fmtDurationMs(pendingStart), true);
    });

    // ── Save Segment Helper (Lưu tạm vào bộ nhớ) ──
    function saveSegment() {
        var d = getDuration();
        if (!d || isSavingSegment) return Promise.resolve(false);
        if (pendingStart === null) {
            showToast(' Hãy đặt điểm bắt đầu trước.');
            return Promise.resolve(false);
        }

        var t = videoEl.currentTime;
        var start = Math.min(pendingStart, t);
        var end = Math.max(pendingStart, t);

        if (end - start < 0.1) {
            showToast(' Thao tác quá ngắn (dưới 0.1 giây).');
            return Promise.resolve(false);
        }
        if (hasOverlap(start, end, null, segType)) {
            showToast(' Thao tác đè lên mốc cùng loại đã có. Vui lòng chọn vùng khác!');
            pendingEnd = t;
            updateSel();
            render();
            return Promise.resolve(false);
        }

        var color = segType === 'incident'
            ? INCIDENT_COLOR
            : (selectedNormalColor || '#38bdf8');

        var labelInput = G('label-input');
        var labelText = labelInput ? labelInput.value.trim() : '';

        var newSeg = {
            id: 'temp_' + Date.now() + '_' + Math.floor(Math.random() * 1000),
            videoSessionId: VIDEO_ID,
            label: labelText,
            startTime: start,
            endTime: end,
            duration: end - start,
            type: segType,
            color: color
        };

        segments.push(newSeg);
        segments.sort(function (a, b) { return a.startTime - b.startTime; });
        pendingStart = null;
        pendingEnd = null;
        if (labelInput) labelInput.value = '';
        var overlapW = G('overlap-warn');
        if (overlapW) overlapW.style.display = 'none';
        setDirty(true);
        updateSel();
        render();
        showToast(' Đã thêm thao tác ' + (newSeg.type === 'incident' ? 'Sai ' : 'Đúng ') + ': ' + fmtDurationMs(newSeg.startTime) + ' ➔ ' + fmtDurationMs(newSeg.endTime), true);
        return Promise.resolve(true);
    }

    // ── Real-time Quick Marker (1-touch Toggle & Continuous Marking) ──
    function handleRealtimeMark(targetType) {
        var d = getDuration();
        if (!d || isSavingSegment) return;
        var now = Date.now();
        if (now - lastMarkActionTime < MARK_COOLDOWN_MS) return;
        lastMarkActionTime = now;

        // Nếu chưa có điểm bắt đầu: Bắt đầu ngay mốc thời gian này
        if (pendingStart === null) {
            setType(targetType);
            pendingStart = videoEl.currentTime;
            pendingEnd = null;
            overlapW.style.display = 'none';
            updateSel();
            render();
            showToast((targetType === 'incident' ? ' Bắt đầu mốc Sai: ' : ' Bắt đầu mốc Đúng: ') + fmtDurationMs(pendingStart), true);
            return;
        }

        // Nếu đang có điểm bắt đầu cùng loại: Bấm lần 2 để chốt & lưu
        if (segType === targetType) {
            saveSegment();
            return;
        }

        // Nếu đang có điểm bắt đầu loại khác: Chốt đoạn cũ và lập tức mở đoạn mới liền mạch
        var currentT = videoEl.currentTime;
        saveSegment().then(function (ok) {
            if (ok) {
                setType(targetType);
                pendingStart = currentT;
                pendingEnd = null;
                overlapW.style.display = 'none';
                updateSel();
                render();
                showToast((targetType === 'incident' ? ' Bắt đầu mốc Sai: ' : ' Bắt đầu mốc Đúng: ') + fmtDurationMs(pendingStart), true);
            }
        });
    }

    // ── Set End (validate + save in-memory) ────────────────────────────
    G('btn-set-end').addEventListener('click', function () {
        var now = Date.now();
        if (now - lastMarkActionTime < MARK_COOLDOWN_MS) return;
        lastMarkActionTime = now;
        saveSegment();
    });

    // ── Delete a segment (In-memory) ──────────────────────────────────
    function deleteSegment(id) {
        segments = segments.filter(function (s) { return String(s.id) !== String(id); });
        setDirty(true);
        render();
        showToast(' Đã xóa thao tác khỏi danh sách.', false);
    }

    // ── Edit label modal ──────────────────────────────────────────────
    function openEditModal(id) {
        var seg = segments.find(function (s) { return String(s.id) === String(id); });
        if (!seg) return;
        editingId = id;
        editInput.value = seg.label || '';
        editModal.classList.add('show');
        editInput.focus();
    }

    G('modal-save').addEventListener('click', function () {
        var seg = segments.find(function (s) { return String(s.id) === String(editingId); });
        if (!seg) { editModal.classList.remove('show'); return; }
        var newLabel = editInput.value.trim();
        seg.label = newLabel;
        setDirty(true);
        render();
        showToast(' Đã cập nhật tên thao tác.', true);
        editModal.classList.remove('show');
        editingId = null;
    });


    G('modal-cancel').addEventListener('click', function () {
        editModal.classList.remove('show'); editingId = null;
    });
    editModal.addEventListener('click', function (e) {
        if (e.target === editModal) { editModal.classList.remove('show'); editingId = null; }
    });
    editInput.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') G('modal-save').click();
        if (e.key === 'Escape') G('modal-cancel').click();
    });

    // ── Custom Confirm Modal ──────────────────────────────────────────
    function confirmAsync(message, title, okText, cancelText) {
        title = title || 'Yêu cầu xác nhận';
        okText = okText || 'Xác nhận xóa';
        cancelText = cancelText || 'Hủy';

        return new Promise(function (resolve) {
            var modal = G('confirm-modal');
            var titleEl = G('confirm-modal-title');
            var msgEl = G('confirm-modal-msg');
            var okBtn = G('confirm-modal-ok');
            var cancelBtn = G('confirm-modal-cancel');

            if (!modal || !titleEl || !msgEl || !okBtn || !cancelBtn) {
                resolve(window.confirm(message));
                return;
            }

            titleEl.textContent = title;
            msgEl.textContent = message;
            okBtn.textContent = okText;
            cancelBtn.textContent = cancelText;

            function cleanup(val) {
                modal.classList.remove('show');
                okBtn.removeEventListener('click', onOk);
                cancelBtn.removeEventListener('click', onCancel);
                modal.removeEventListener('click', onBackdrop);
                document.removeEventListener('keydown', onKey);
                resolve(val);
            }

            function onOk() { cleanup(true); }
            function onCancel() { cleanup(false); }
            function onBackdrop(e) { if (e.target === modal) cleanup(false); }
            function onKey(e) {
                if (!modal.classList.contains('show')) return;
                if (e.key === 'Escape') {
                    e.preventDefault();
                    cleanup(false);
                } else if (e.key === 'Enter') {
                    e.preventDefault();
                    cleanup(true);
                }
            }

            okBtn.addEventListener('click', onOk);
            cancelBtn.addEventListener('click', onCancel);
            modal.addEventListener('click', onBackdrop);
            document.addEventListener('keydown', onKey);

            modal.classList.add('show');
            okBtn.focus();
        });
    }

    // ── Type selector ─────────────────────────────────────────────────
    window.setType = function (type) {
        segType = type;
        G('type-normal').className = 'type-btn' + (type === 'normal' ? ' on-normal' : '');
        G('type-incident').className = 'type-btn' + (type === 'incident' ? ' on-incident' : '');

        var colorBlock = G('block-normal-color');
        if (colorBlock) {
            if (type === 'normal') {
                colorBlock.style.opacity = '1';
                colorBlock.style.pointerEvents = 'auto';
            } else {
                colorBlock.style.opacity = '0.45';
                colorBlock.style.pointerEvents = 'none';
            }
        }
    };

    // ── Timeline click → seek ─────────────────────────────────────────
    tlWrap.addEventListener('click', function (e) {
        if (!duration) return;
        var rect = tlWrap.getBoundingClientRect();
        videoEl.currentTime = pxToTime(e.clientX - rect.left);
    });

    // Hover tooltip
    tlWrap.addEventListener('mousemove', function (e) {
        if (!duration) return;
        var rect = tlWrap.getBoundingClientRect();
        tooltip.style.display = 'block';
        tooltip.style.left = (e.clientX + 14) + 'px';
        tooltip.style.top = (e.clientY - 34) + 'px';
        tooltip.textContent = fmtDurationMs(pxToTime(e.clientX - rect.left));
    });
    tlWrap.addEventListener('mouseleave', function () {
        tooltip.style.display = 'none';
        if (segPopup) segPopup.style.display = 'none';
    });
    window.addEventListener('scroll', function () {
        if (segPopup) segPopup.style.display = 'none';
    }, true);

    // ── Playhead sync ─────────────────────────────────────────────────
    videoEl.addEventListener('timeupdate', function () {
        if (!duration) return;
        playheadEl.style.left = toPct(videoEl.currentTime) + '%';
        ctDisp.textContent = fmtDurationMs(videoEl.currentTime);
    });

    videoEl.addEventListener('loadedmetadata', function () {
        if (videoEl.duration && videoEl.duration > 0) {
            duration = videoEl.duration;
            durDisp.textContent = fmtDurationMs(duration);
            buildTicks();
            render(); // Gọi render lại khi độ dài video đã sẵn sàng

            // Lưu thời lượng thực tế của video vào CSDL nếu chưa có
            if (!window.VIDEO_DURATION || window.VIDEO_DURATION <= 0) {
                window.VIDEO_DURATION = duration;
                apiUpdateDuration(duration);
            }
        }
    });

    // ── Toolbar buttons ───────────────────────────────────────────────
    G('btn-save-db').addEventListener('click', function () {
        if (!isDirty) {
            showToast(' Dữ liệu đã ở trạng thái đồng bộ với CSDL.', true);
            return;
        }

        confirmAsync(
            'Bạn có chắc chắn muốn lưu toàn bộ ' + segments.length + ' thao tác vào cơ sở dữ liệu không?',
            'Yêu cầu xác nhận',
            'Lưu vào CSDL',
            'Hủy'
        ).then(function (ok) {
            if (!ok) return;

            setSaving(true);
            apiSaveBatch(VIDEO_ID, segments).then(function (res) {
                if (res.status === 409) {
                    return res.json().then(function (err) {
                        showToast(+ (err.message || 'Có thao tác bị đè thời gian lên nhau.'));
                    });
                }
                if (!res.ok) {
                    showToast(' Lỗi khi lưu vào cơ sở dữ liệu.');
                    return;
                }
                return res.json().then(function (savedList) {
                    initialSegments = savedList.map(function (s) { return Object.assign({}, s); });
                    segments = savedList.map(function (s) { return Object.assign({}, s); });
                    try { localStorage.removeItem(DRAFT_STORAGE_KEY); } catch (e) { }
                    setDirty(false);
                    render();
                    showToast(' Đã lưu toàn bộ thao tác vào CSDL thành công!', true);
                });
            }).catch(function () {
                showToast(' Lỗi kết nối đến máy chủ.');
            }).then(function () {
                setSaving(false);
            });
        });
    });

    G('btn-discard-db').addEventListener('click', function () {
        if (!isDirty) {
            showToast(' Không có thay đổi nào chưa lưu.');
            return;
        }

        confirmAsync(
            'Bạn có chắc chắn muốn hủy tất cả các thay đổi chưa lưu và khôi phục về trạng thái ban đầu trong cơ sở dữ liệu không?',
            'Yêu cầu xác nhận',
            'Hủy thay đổi',
            'Giữ lại'
        ).then(function (ok) {
            if (!ok) return;
            segments = initialSegments.map(function (s) { return Object.assign({}, s); });
            try { localStorage.removeItem(DRAFT_STORAGE_KEY); } catch (e) { }
            setDirty(false);
            render();
            showToast('Đã hủy bỏ các thay đổi và khôi phục dữ liệu CSDL ban đầu.');
        });
    });

    var btnPlay = G('btn-play');
    if (btnPlay) {
        btnPlay.addEventListener('click', function () {
            videoEl.paused ? videoEl.play() : videoEl.pause();
        });
    }


    G('btn-undo').addEventListener('click', function () {
        if (!segments.length) { showToast(' Không có thao tác nào để hoàn tác.'); return; }
        var last = segments[segments.length - 1];
        var desc = (last.type === 'incident' ? 'thao tác Sai' : 'thao tác Đúng') + (last.label ? ' "' + last.label + '"' : '') + ' (' + fmtDurationMs(last.startTime) + ' -> ' + fmtDurationMs(last.endTime) + ')';
        confirmAsync('Bạn có chắc muốn hoàn tác (Undo) và xóa ' + desc + ' không?', 'Yêu cầu xác nhận', 'Hoàn tác & Xóa', 'Hủy').then(function (ok) {
            if (ok) deleteSegment(last.id);
        });
    });

    G('btn-clear').addEventListener('click', function () {
        if (!segments.length) { showToast(' Không có thao tác nào để xóa.'); return; }
        confirmAsync('CẢNH BÁO: Bạn có chắc chắn muốn xóa toàn bộ ' + segments.length + ' thao tác đã đánh dấu không?\n\nThao tác này sẽ xóa tạm thời trên giao diện cho đến khi bạn bấm Lưu vào CSDL.', 'Yêu cầu xác nhận', 'Xóa tất cả', 'Hủy').then(function (ok) {
            if (!ok) return;
            segments = [];
            pendingStart = null;
            pendingEnd = null;
            overlapW.style.display = 'none';
            setDirty(true);
            updateSel();
            render();
            showToast(' Đã xóa tất cả thao tác.');
        });
    });



    // ── Intercept link navigation with custom Confirm Modal when unsaved ──
    document.addEventListener('click', function (e) {
        var a = e.target.closest('a');
        if (!a || !isDirty || !a.href || a.getAttribute('target') === '_blank') return;
        var targetUrl = a.href;
        if (targetUrl.indexOf('#') === targetUrl.length - 1 || targetUrl.startsWith('javascript:')) return;

        e.preventDefault();
        confirmAsync(
            'Bạn có các thay đổi chưa được lưu vào cơ sở dữ liệu. Bạn có chắc chắn muốn rời khỏi trang không?',
            'Xác nhận rời khỏi trang',
            'Rời khỏi trang',
            'Giữ lại'
        ).then(function (ok) {
            if (ok) {
                isDirty = false;
                window.location.href = targetUrl;
            }
        });
    });

    // ── Warn before leaving with unsaved changes ──────────────────────
    window.addEventListener('beforeunload', function (e) {
        if (isDirty) {
            e.preventDefault();
            e.returnValue = 'Bạn có các thay đổi chưa được lưu vào cơ sở dữ liệu. Bạn có chắc muốn rời đi?';
            return e.returnValue;
        }
    });

    // ── Keyboard shortcuts ────────────────────────────────────────────
    document.addEventListener('keydown', function (e) {
        var d = getDuration();
        if (!d) return;
        if (['INPUT', 'TEXTAREA'].indexOf(document.activeElement.tagName) !== -1) return;

        // Anti-spam: Bỏ qua khi người dùng giữ đè phím (auto-repeat)
        if (e.repeat) return;

        var key = (e.key || '').toLowerCase();
        var code = e.code || '';

        // Space: Play / Pause
        if (code === 'Space' || key === ' ' || e.keyCode === 32) {
            e.preventDefault();
            videoEl.paused ? videoEl.play() : videoEl.pause();
            return;
        }

        // Q hoặc 1: Đúng (Normal)
        if (code === 'KeyQ' || key === 'q' || code === 'Digit1' || code === 'Numpad1' || key === '1') {
            e.preventDefault();
            handleRealtimeMark('normal');
            return;
        }

        // W hoặc 2: Sai (Incident)
        if (code === 'KeyW' || key === 'w' || code === 'Digit2' || code === 'Numpad2' || key === '2') {
            e.preventDefault();
            handleRealtimeMark('incident');
            return;
        }

        // S: Đặt điểm đầu
        if (code === 'KeyS' || key === 's') {
            e.preventDefault();
            G('btn-set-start').click();
            return;
        }

        // E: Đặt điểm cuối
        if (code === 'KeyE' || key === 'e') {
            e.preventDefault();
            G('btn-set-end').click();
            return;
        }

        // Escape: Hủy mốc đang chọn
        if (code === 'Escape' || key === 'escape' || e.keyCode === 27) {
            e.preventDefault();
            if (pendingStart !== null) {
                pendingStart = null;
                pendingEnd = null;
                overlapW.style.display = 'none';
                updateSel();
                render();
                showToast(' Đã hủy mốc đang chọn');
            }
            return;
        }

        // ArrowLeft: Tua lùi 5s
        if (code === 'ArrowLeft' || key === 'arrowleft') {
            e.preventDefault();
            videoEl.currentTime = Math.max(0, videoEl.currentTime - 5);
            return;
        }

        // ArrowRight: Tua tới 5s
        if (code === 'ArrowRight' || key === 'arrowright') {
            e.preventDefault();
            videoEl.currentTime = Math.min(d, videoEl.currentTime + 5);
            return;
        }
    });

    // ── Mouse Drag-to-Scroll for Table (cho phép kéo bảng bằng chuột trên màn hình nhỏ) ──
    var tblWrap = document.querySelector('.seg-table-wrap');
    if (tblWrap) {
        var isDown = false;
        var startX, scrollLeft;

        tblWrap.addEventListener('mousedown', function (e) {
            // Không can thiệp nếu đang click vào ô input hoặc nút bấm
            if (['INPUT', 'BUTTON', 'A'].indexOf(e.target.tagName) !== -1) return;
            isDown = true;
            tblWrap.classList.add('grabbing');
            startX = e.pageX - tblWrap.offsetLeft;
            scrollLeft = tblWrap.scrollLeft;
        });

        document.addEventListener('mouseup', function () {
            if (isDown) {
                isDown = false;
                tblWrap.classList.remove('grabbing');
            }
        });

        tblWrap.addEventListener('mousemove', function (e) {
            if (!isDown) return;
            e.preventDefault();
            var x = e.pageX - tblWrap.offsetLeft;
            var walk = (x - startX) * 1.5;
            tblWrap.scrollLeft = scrollLeft - walk;
        });
    }

    // ── Start ─────────────────────────────────────────────────────────
    init();

})();
