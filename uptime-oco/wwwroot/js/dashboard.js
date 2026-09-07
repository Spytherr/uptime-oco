document.addEventListener('DOMContentLoaded', function () {
    if (typeof ApexCharts === 'undefined') {
        return;
    }

    var data = window.dashboardData || window.monitorDetailsData;
    if (!data) {
        return;
    }

    var isDashboard = Boolean(window.dashboardData);
    var responseTimeEl = document.querySelector('#responseTimeChart, #monitorResponseTimeChart');
    var responseTimeChart;
    var statusCodeChart;
    var currentResponseTimeSeries = [];
    var monitorSelect = document.querySelector('#responseTimeMonitorSelect');

    function escapeHtml(value) {
        return String(value).replace(/[&<>"']/g, function (character) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#039;'
            }[character];
        });
    }

    function getMonitorIntervalSeconds(monitorId) {
        var monitor = (data.monitors || []).find(function (item) {
            return String(item.id) === String(monitorId);
        });
        var intervalSeconds = monitor ? Number(monitor.intervalSeconds) : 60;

        return Number.isFinite(intervalSeconds) && intervalSeconds > 0 ? intervalSeconds : 60;
    }

    function toChartPoints(points) {
        return points
            .map(function (point) {
                return {
                    x: new Date(point.checkedAt).getTime(),
                    y: point.responseTimeMs,
                    monitorId: point.monitorId,
                    monitorName: point.monitorName
                };
            })
            .filter(function (point) {
                return Number.isFinite(point.x) && Number.isFinite(point.y);
            })
            .sort(function (a, b) { return a.x - b.x; });
    }

    function getGapThresholdMs(previousPoint, nextPoint) {
        var previousIntervalMs = getMonitorIntervalSeconds(previousPoint.monitorId) * 1000;
        var nextIntervalMs = getMonitorIntervalSeconds(nextPoint.monitorId) * 1000;
        var expectedIntervalMs = Math.max(previousIntervalMs, nextIntervalMs);

        return Math.max(5 * 60 * 1000, expectedIntervalMs * 4);
    }

    function addGapBreaks(points) {
        var pointsWithGaps = [];

        points.forEach(function (point, index) {
            var previousPoint = points[index - 1];

            if (previousPoint && point.x - previousPoint.x > getGapThresholdMs(previousPoint, point)) {
                pointsWithGaps.push({
                    x: Math.floor(previousPoint.x + (point.x - previousPoint.x) / 2),
                    y: null
                });
            }

            pointsWithGaps.push(point);
        });

        return pointsWithGaps;
    }

    function getSelectedMonitorId() {
        if (monitorSelect) {
            return monitorSelect.value;
        }

        return data.monitorId != null ? String(data.monitorId) : '';
    }

    function getResponseTimeSeries(monitorId) {
        var points = data.responseTime || [];
        var pointsByMonitor = {};

        if (monitorId !== '') {
            points = points.filter(function (point) {
                return String(point.monitorId) === String(monitorId);
            });
        }

        points.forEach(function (point) {
            var key = String(point.monitorId);
            if (!pointsByMonitor[key]) {
                pointsByMonitor[key] = [];
            }
            pointsByMonitor[key].push(point);
        });

        return Object.keys(pointsByMonitor)
            .map(function (key) {
                var monitorPoints = toChartPoints(pointsByMonitor[key]);
                return {
                    name: monitorPoints[0] && monitorPoints[0].monitorName
                        ? monitorPoints[0].monitorName
                        : 'Monitor ' + key,
                    data: addGapBreaks(monitorPoints)
                };
            })
            .sort(function (a, b) { return a.name.localeCompare(b.name); });
    }

    function getAllSeriesPoints(series) {
        return series
            .reduce(function (points, item) { return points.concat(item.data); }, [])
            .filter(function (point) { return Number.isFinite(point.x); })
            .sort(function (a, b) { return a.x - b.x; });
    }

    function resetResponseTimeZoom() {
        var points = getAllSeriesPoints(currentResponseTimeSeries);
        if (!responseTimeChart || points.length < 2) {
            return;
        }

        var first = points[0].x;
        var last = points[points.length - 1].x;

        if (first < last) {
            responseTimeChart.zoomX(first, last);
        }
    }

    if (responseTimeEl && data.responseTime) {
        currentResponseTimeSeries = getResponseTimeSeries(getSelectedMonitorId());

        responseTimeChart = new ApexCharts(responseTimeEl, {
            chart: {
                type: 'line',
                height: 300,
                background: 'transparent',
                toolbar: {
                    show: true,
                    tools: {
                        zoom: true,
                        zoomin: true,
                        zoomout: true,
                        pan: false,
                        reset: false,
                        download: false
                    },
                    color: '#929292',
                    background: '#202020'
                },
                zoom: {
                    enabled: true,
                    type: 'x'
                },
                animations: {
                    enabled: false,
                    animateGradually: {
                        enabled: false
                    },
                    dynamicAnimation: {
                        enabled: false
                    }
                }
            },
            series: currentResponseTimeSeries,
            colors: ['#c9c9c9', '#22c55e', '#f59e0b', '#60a5fa', '#f472b6', '#a78bfa'],
            legend: {
                show: currentResponseTimeSeries.length > 1,
                position: 'bottom',
                labels: { colors: '#929292' }
            },
            xaxis: {
                type: 'datetime',
                tickAmount: 8,
                labels: {
                    datetimeUTC: false,
                    hideOverlappingLabels: true,
                    rotate: 0,
                    trim: true,
                    style: { colors: '#929292' }
                },
                axisBorder: { show: false }
            },
            yaxis: {
                labels: { style: { colors: '#929292' } }
            },
            stroke: {
                curve: 'straight',
                width: 2,
                connectNullData: false
            },
            markers: {
                size: 0,
                hover: { size: 5 }
            },
            grid: {
                borderColor: '#4a4a4a',
                strokeDashArray: 0
            },
            tooltip: {
                theme: 'dark',
                x: { format: 'dd MMM HH:mm:ss' },
                y: { formatter: function (val) { return val + ' ms'; } },
                custom: function (opts) {
                    var point = opts.w.config.series[opts.seriesIndex].data[opts.dataPointIndex];
                    var monitor = point && point.monitorName
                        ? '<div style="color: #929292; font-size: 12px;">' + escapeHtml(point.monitorName) + '</div>'
                        : '';
                    var value = point ? point.y : '';
                    return '<div style="padding: 8px 10px;">' + monitor + '<strong>' + value + ' ms</strong></div>';
                }
            }
        });
        responseTimeChart.render();

        var resetBtn = document.querySelector('#resetZoomBtn');
        if (resetBtn) {
            resetBtn.addEventListener('click', resetResponseTimeZoom);
        }

        if (monitorSelect) {
            monitorSelect.addEventListener('change', function () {
                currentResponseTimeSeries = getResponseTimeSeries(getSelectedMonitorId());
                responseTimeChart.updateSeries(currentResponseTimeSeries);
                setTimeout(resetResponseTimeZoom, 0);
            });
        }
    }

    var statusCodeEl = document.querySelector('#statusCodeChart');
    if (statusCodeEl && data.statusCodes && data.statusCodes.length > 0) {
        statusCodeChart = new ApexCharts(statusCodeEl, {
            chart: {
                type: 'donut',
                height: 300,
                background: 'transparent'
            },
            series: data.statusCodes.map(function (d) { return d.count; }),
            labels: data.statusCodes.map(function (d) { return d.statusCode.toString(); }),
            colors: ['#22c55e', '#ef4444', '#f59e0b', '#94a3b8', '#64748b'],
            legend: {
                position: 'bottom',
                labels: { colors: '#929292' }
            },
            tooltip: {
                theme: 'dark',
                custom: function (opts) {
                    var idx = opts.seriesIndex;
                    var sc = data.statusCodes[idx];
                    var monitors = sc.monitors || [];

                    var html = '<div style="padding: 10px; min-width: 200px;">';
                    html += '<div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">';
                    html += '<b style="font-size: 14px;">HTTP ' + sc.statusCode + '</b>';
                    html += '<span style="color: #929292; font-size: 13px;">' + sc.count + ' total</span>';
                    html += '</div>';
                    html += '<div style="border-top: 2px solid #4a4a4a; margin-bottom: 6px;"></div>';

                    monitors.forEach(function (m) {
                        html += '<div style="display: flex; justify-content: space-between; font-size: 12px; padding: 3px 0;">';
                        html += '<span>' + escapeHtml(m.monitorName) + '</span>';
                        html += '<span style="color: #929292;">' + m.count + '</span>';
                        html += '</div>';
                    });

                    html += '</div>';
                    return html;
                }
            }
        });
        statusCodeChart.render();
    }

    function showToast(title, message, type) {
        var container = document.querySelector('#toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'fixed bottom-4 right-4 z-50 space-y-2';
            document.body.appendChild(container);
        }

        var bgColor = type === 'down' ? 'bg-oco-down/90' : 'bg-oco-up/90';
        var icon = type === 'down' ? '\uD83D\uDD34' : '\u2705';

        var toast = document.createElement('div');
        toast.className = bgColor + ' text-white border-2 border-oco-border px-4 py-3 max-w-sm transition-all duration-300 opacity-0 translate-y-2';
        toast.innerHTML = '<div class="flex items-start gap-3">' +
            '<span class="text-lg">' + icon + '</span>' +
            '<div><p class="font-medium text-sm">' + escapeHtml(title) + '</p>' +
            '<p class="text-sm opacity-90 mt-1">' + escapeHtml(message) + '</p></div></div>';

        container.appendChild(toast);

        requestAnimationFrame(function () {
            toast.classList.remove('opacity-0', 'translate-y-2');
        });

        setTimeout(function () {
            toast.classList.add('opacity-0', 'translate-y-2');
            setTimeout(function () { toast.remove(); }, 300);
        }, 5000);
    }

    function getStatusPresentation(status) {
        var normalizedStatus = String(status || 'Unknown').toUpperCase();

        switch (normalizedStatus) {
            case 'UP':
                return {
                    badge: 'bg-oco-up/10 text-oco-up',
                    dot: 'bg-oco-up',
                    label: 'UP'
                };
            case 'DOWN':
                return {
                    badge: 'bg-oco-down/10 text-oco-down',
                    dot: 'bg-oco-down',
                    label: 'DOWN'
                };
            case 'PAUSED':
                return {
                    badge: 'bg-oco-muted/10 text-oco-muted',
                    dot: 'bg-oco-muted',
                    label: 'PAUSED'
                };
            default:
                return {
                    badge: 'bg-oco-muted/10 text-oco-muted',
                    dot: 'bg-oco-muted',
                    label: 'UNKNOWN'
                };
        }
    }

    function updateMonitorCard(payload) {
        var cards = document.querySelectorAll('[data-monitor-id]');
        cards.forEach(function (card) {
            if (String(card.dataset.monitorId) !== String(payload.monitorId)) {
                return;
            }

            var badge = card.querySelector('[data-status-badge]');
            var dot = card.querySelector('[data-status-dot]');
            var label = card.querySelector('[data-status-label]');
            if (badge && dot && label) {
                var animationClasses = dot.className.split(' ').filter(function (c) { return c.startsWith('animate-'); }).join(' ');
                var status = payload.status || (payload.isSuccess ? 'Up' : 'Down');
                var presentation = getStatusPresentation(status);
                badge.className = 'inline-flex items-center gap-2 px-3 py-1 border-2 border-oco-border text-sm font-medium ' + presentation.badge;
                dot.className = 'w-2 h-2 ' + presentation.dot + ' ' + animationClasses;
                label.className = 'text-sm font-medium ' + presentation.dot.replace('bg-', 'text-');
                label.textContent = presentation.label;
            }

            var lastCheck = card.querySelector('[data-last-check]');
            if (lastCheck && payload.checkedAt) {
                var dt = new Date(payload.checkedAt);
                lastCheck.textContent = 'Last check: ' + dt.toLocaleTimeString();
            }

            var respTime = card.querySelector('[data-response-time]');
            if (respTime) {
                respTime.textContent = payload.responseTimeMs != null ? payload.responseTimeMs + ' ms' : '';
            }

            card.classList.add('ring-2', 'ring-oco-accent', 'ring-offset-2', 'ring-offset-oco-surface');
            setTimeout(function () {
                card.classList.remove('ring-2', 'ring-oco-accent', 'ring-offset-2', 'ring-offset-oco-surface');
            }, 1000);
        });
    }

    if (typeof signalR !== 'undefined') {
        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/monitor')
            .withAutomaticReconnect()
            .build();

        connection.on('PingReceived', function (payload) {
            updateMonitorCard(payload);

            if (responseTimeChart && payload.responseTimeMs != null) {
                var selectedId = getSelectedMonitorId();
                var monitorMatches = selectedId === '' || String(selectedId) === String(payload.monitorId);
                var point = {
                    checkedAt: payload.checkedAt,
                    responseTimeMs: payload.responseTimeMs,
                    monitorId: payload.monitorId,
                    monitorName: payload.monitorName
                };

                if (isDashboard || monitorMatches) {
                    data.responseTime.push(point);
                    var cutoff = Date.now() - 24 * 60 * 60 * 1000;
                    data.responseTime = data.responseTime.filter(function (item) {
                        return new Date(item.checkedAt).getTime() >= cutoff;
                    });
                }

                if (monitorMatches) {
                    var globals = responseTimeChart.w.globals;
                    var zoomMin = globals.minX;
                    var zoomMax = globals.maxX;
                    currentResponseTimeSeries = getResponseTimeSeries(selectedId);
                    responseTimeChart.updateSeries(currentResponseTimeSeries, false);

                    if (Number.isFinite(zoomMin) && Number.isFinite(zoomMax) && zoomMax > zoomMin) {
                        responseTimeChart.zoomX(zoomMin, zoomMax);
                    }
                }
            }

            if (statusCodeChart && payload.httpStatusCode != null) {
                var sc = data.statusCodes.find(function (d) { return d.statusCode === payload.httpStatusCode; });
                if (!sc) {
                    sc = { statusCode: payload.httpStatusCode, count: 0, monitors: [] };
                    data.statusCodes.push(sc);
                }
                sc.count++;

                var monitorEntry = sc.monitors.find(function (m) { return m.monitorName === payload.monitorName; });
                if (!monitorEntry) {
                    monitorEntry = { monitorName: payload.monitorName, count: 0 };
                    sc.monitors.push(monitorEntry);
                }
                monitorEntry.count++;

                data.statusCodes.sort(function (a, b) { return b.count - a.count; });

                statusCodeChart.updateSeries(
                    data.statusCodes.map(function (d) { return d.count; }),
                    false
                );
                statusCodeChart.updateOptions({
                    labels: data.statusCodes.map(function (d) { return d.statusCode.toString(); })
                });
            }
        });

        connection.on('IncidentUpdate', function (payload) {
            var title = payload.isResolved ? 'Monitor Recovered' : 'Monitor Down';
            var message = payload.monitorName + (payload.isResolved ? ' is back up' : ' is down') + ' — ' + payload.reason;
            showToast(title, message, payload.isResolved ? 'up' : 'down');
        });

        connection.start().catch(function (err) {
            console.error('SignalR connection failed:', err);
        });
    }
});
