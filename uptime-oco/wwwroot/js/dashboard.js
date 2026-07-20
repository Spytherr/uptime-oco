document.addEventListener('DOMContentLoaded', function () {
    if (typeof ApexCharts === 'undefined') {
        return;
    }

    var data = window.dashboardData;
    if (!data) {
        return;
    }

    var responseTimeEl = document.querySelector('#responseTimeChart');
    var responseTimeChart;
    var currentResponseTimePoints = [];

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

    function getResponseTimePoints(monitorId) {
        var points = data.responseTime || [];

        if (monitorId !== '') {
            points = points.filter(function (point) {
                return String(point.monitorId) === String(monitorId);
            });
        }

        return addGapBreaks(toChartPoints(points));
    }

    function resetResponseTimeZoom() {
        if (!responseTimeChart || currentResponseTimePoints.length < 2) {
            return;
        }

        var first = currentResponseTimePoints[0].x;
        var last = currentResponseTimePoints[currentResponseTimePoints.length - 1].x;

        if (first < last) {
            responseTimeChart.zoomX(first, last);
        }
    }

    function selectedMonitorName(monitorId) {
        if (monitorId === '') {
            return 'Response Time (all monitors)';
        }

        var selectedPoint = (data.responseTime || []).find(function (point) {
            return String(point.monitorId) === String(monitorId);
        });

        return selectedPoint ? 'Response Time - ' + selectedPoint.monitorName : 'Response Time';
    }

    if (responseTimeEl && data.responseTime) {
        currentResponseTimePoints = getResponseTimePoints('');

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
                    color: '#94a3b8',
                    background: '#1e293b'
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
            series: [{
                name: 'Response Time (all monitors)',
                data: currentResponseTimePoints
            }],
            xaxis: {
                type: 'datetime',
                tickAmount: 8,
                labels: {
                    datetimeUTC: false,
                    hideOverlappingLabels: true,
                    rotate: 0,
                    trim: true,
                    style: { colors: '#94a3b8' }
                },
                axisBorder: { show: false }
            },
            yaxis: {
                labels: { style: { colors: '#94a3b8' } }
            },
            stroke: {
                curve: 'smooth',
                width: 2,
                colors: ['#6366f1'],
                connectNullData: false
            },
            markers: {
                size: 0,
                hover: { size: 5 }
            },
            grid: {
                borderColor: '#334155',
                strokeDashArray: 3
            },
            tooltip: {
                theme: 'dark',
                x: { format: 'dd MMM HH:mm:ss' },
                y: { formatter: function (val) { return val + ' ms'; } },
                custom: function (opts) {
                    var point = opts.w.config.series[opts.seriesIndex].data[opts.dataPointIndex];
                    var monitor = point && point.monitorName
                        ? '<div style="color: #94a3b8; font-size: 12px;">' + escapeHtml(point.monitorName) + '</div>'
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

        var monitorSelect = document.querySelector('#responseTimeMonitorSelect');
        if (monitorSelect) {
            monitorSelect.addEventListener('change', function () {
                currentResponseTimePoints = getResponseTimePoints(monitorSelect.value);
                responseTimeChart.updateSeries([{
                    name: selectedMonitorName(monitorSelect.value),
                    data: currentResponseTimePoints
                }]);
                setTimeout(resetResponseTimeZoom, 0);
            });
        }
    }

    var statusCodeEl = document.querySelector('#statusCodeChart');
    if (statusCodeEl && data.statusCodes && data.statusCodes.length > 0) {
        var statusCodeChart = new ApexCharts(statusCodeEl, {
            chart: {
                type: 'donut',
                height: 300,
                background: 'transparent'
            },
            series: data.statusCodes.map(function (d) { return d.count; }),
            labels: data.statusCodes.map(function (d) { return d.statusCode.toString(); }),
            colors: ['#22c55e', '#ef4444', '#f59e0b', '#6366f1', '#94a3b8'],
            legend: {
                position: 'bottom',
                labels: { colors: '#94a3b8' }
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
                    html += '<span style="color: #94a3b8; font-size: 13px;">' + sc.count + ' total</span>';
                    html += '</div>';
                    html += '<div style="border-top: 1px solid #334155; margin-bottom: 6px;"></div>';

                    monitors.forEach(function (m) {
                        html += '<div style="display: flex; justify-content: space-between; font-size: 12px; padding: 3px 0;">';
                        html += '<span>' + m.monitorName + '</span>';
                        html += '<span style="color: #94a3b8;">' + m.count + '</span>';
                        html += '</div>';
                    });

                    html += '</div>';
                    return html;
                }
            }
        });
        statusCodeChart.render();
    }
});
