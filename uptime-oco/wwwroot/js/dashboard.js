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
                toolbar: { show: false }
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
                theme: 'dark'
            }
        });
        statusCodeChart.render();
    }
});
