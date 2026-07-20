document.addEventListener('DOMContentLoaded', function () {
    if (typeof ApexCharts === 'undefined') {
        return;
    }

    var data = window.dashboardData;
    if (!data) {
        return;
    }

    var responseTimeEl = document.querySelector('#responseTimeChart');
    if (responseTimeEl && data.responseTime) {
        var responseTimeChart = new ApexCharts(responseTimeEl, {
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
                    enabled: true
                }
            },
            series: [{
                name: 'Response Time (ms)',
                data: data.responseTime.map(function (d) { return d.y; })
            }],
            xaxis: {
                categories: data.responseTime.map(function (d) { return d.x; }),
                labels: { style: { colors: '#94a3b8' } },
                axisBorder: { show: false }
            },
            yaxis: {
                labels: { style: { colors: '#94a3b8' } }
            },
            stroke: {
                curve: 'smooth',
                width: 2,
                colors: ['#6366f1']
            },
            grid: {
                borderColor: '#334155',
                strokeDashArray: 3
            },
            tooltip: {
                theme: 'dark',
                y: { formatter: function (val) { return val + ' ms'; } }
            }
        });
        responseTimeChart.render();

        var resetBtn = document.querySelector('#resetZoomBtn');
        if (resetBtn) {
            resetBtn.addEventListener('click', function () {
                responseTimeChart.resetSeries();
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
