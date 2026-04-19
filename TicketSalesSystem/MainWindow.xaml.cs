using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TicketSalesSystem.Database;
using TicketSalesSystem.Services;
using static TicketSalesSystem.Database.DatabaseHelper;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TicketSalesSystem.Tests")]

namespace TicketSalesSystem
{
    public partial class MainWindow : Window
    {
        private readonly IDatabaseHelper _db = new DatabaseHelper();
        public MainWindow()
        {
            _db = new DatabaseHelper();
            InitializeComponent();
            LoadAnalyticsOptions();
            LoadFilters();
            yearCombo.SelectedIndex = 0;
            monthCombo.SelectedIndex = 0;
            typeCombo.SelectedIndex = 0;
            cityCombo.SelectedIndex = 0;
            managerCombo.SelectedIndex = 0;
        }
        // Конструктор для тестів
        public MainWindow(IDatabaseHelper db)
        {
            _db = db;
            yearCombo = new ComboBox(); monthCombo = new ComboBox(); typeCombo = new ComboBox();
            cityCombo = new ComboBox(); managerCombo = new ComboBox(); analyticsComboBox = new ComboBox();
            salesGrid = new DataGrid(); analyticsGrid = new DataGrid();
            filtersPanel = new StackPanel(); cityPanel = new StackPanel(); managerPanel = new StackPanel();
            totalText = new TextBlock();

            LoadAnalyticsOptions();
        }
        public virtual void LoadSales()
        {
            string query = @"
            SELECT
            s.sale_id,
            e.event_name,
            m.full_name AS manager,
            c.full_name AS client,
            v.venue_name,
            s.quantity,
            s.price,
            s.total_amount,
            s.sale_datetime,
            s.is_returned
            FROM TicketSale s
            JOIN Event e ON s.event_id = e.event_id
            JOIN Manager m ON s.manager_id = m.manager_id
            JOIN Client c ON s.client_id = c.client_id
            JOIN Venue v ON e.venue_id = v.venue_id";
            DataTable table = _db.GetOLTPData(query);
            salesGrid.ItemsSource = table.DefaultView;

        }
        public void LoadSales_Click(object sender, RoutedEventArgs e)
        {
            LoadSales();
        }

        public void AddSale_Click(object sender, RoutedEventArgs e)
        {
            OpenAddSaleWindow();
            LoadSales();
        }

        public void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            if (salesGrid.SelectedItem == null)
            {
                MessageBox.Show("Select a sale first.");
                return;
            }

            DataRowView row = (DataRowView)salesGrid.SelectedItem;

            int saleId = (int)row["sale_id"];
            string eventName = row["event_name"].ToString();
            string manager = row["manager"].ToString();
            string client = row["client"].ToString();
            string amount = row["total_amount"].ToString();

            string message =
            $@"Are you sure you want to delete this sale?

            Event: {eventName}
            Manager: {manager}
            Client: {client}
            Total: {amount}

            This action cannot be undone.";
            var result = ShowConfirmMessage(message, "Confirm Delete");
            if (result != MessageBoxResult.Yes)
                return;

            using var conn = _db.GetOLTPConnection();
            OpenConnection(conn);

            SqlCommand cmd = new SqlCommand(
                "DELETE FROM TicketSale WHERE sale_id=@id", conn);

            cmd.Parameters.AddWithValue("@id", saleId);

            ExecuteCommand(cmd);
            ShowMessage("Sale deleted.");
            LoadSales();
        }
        public void EditSale_Click(object sender, RoutedEventArgs e)
        {
            if (salesGrid.SelectedItem == null)
            {
                MessageBox.Show("Select a sale first.");
                return;
            }

            DataRowView row = (DataRowView)salesGrid.SelectedItem;
            int saleId = (int)row["sale_id"];
            OpenEditSaleWindow(saleId);
            LoadSales();
        }

        public void Transfer_Click(object sender, RoutedEventArgs e)
        {
            RunEtlTransfer();
            ShowMessage("Data transferred to warehouse");
        }

        public virtual void LoadAnalytics(string query)
        {
            analyticsGrid.ItemsSource = _db.GetData(query).DefaultView;
        }
        public void analyticsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (analyticsComboBox.SelectedItem == null)
                return;
            ResetFilters();

            string selected = analyticsComboBox.SelectedItem.ToString();
            string query = "";

            // Ховаємо все по дефолту
            filtersPanel.Visibility = Visibility.Collapsed;
            cityPanel.Visibility = Visibility.Collapsed;
            managerPanel.Visibility = Visibility.Collapsed;
            totalText.Visibility = Visibility.Collapsed;
            totalText.Text = "";

            if (selected.Contains("типами"))
            {
                query = @"
                SELECT 
                    et.type_name,
                    SUM(f.quantity) AS total_tickets,
                    SUM(f.total_amount) AS total_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.EventType et 
                    ON f.event_type_id = et.event_type_id
                GROUP BY et.type_name
                ORDER BY total_revenue DESC";
            }
            else if (selected.Contains("подіями"))
            {
                query = @"
                SELECT 
                    e.event_name,
                    SUM(f.quantity) AS total_tickets,
                    SUM(f.total_amount) AS total_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.Event e 
                    ON f.event_id = e.event_id
                GROUP BY e.event_name
                ORDER BY total_revenue DESC";
            }
            else if (selected.Contains("менеджерами"))
            {
                query = @"
                SELECT 
                    m.full_name,
                    SUM(f.quantity) AS total_tickets,
                    SUM(f.total_amount) AS total_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.Manager m 
                    ON f.manager_id = m.manager_id
                GROUP BY m.full_name
                ORDER BY total_revenue DESC";
            }
            else if (selected.Contains("місцями"))
            {
                query = @"
                SELECT 
                    v.venue_name,
                    v.city,
                    SUM(f.quantity) AS total_tickets,
                    SUM(f.total_amount) AS total_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.Venue v 
                    ON f.venue_id = v.venue_id
                GROUP BY v.venue_name, v.city
                ORDER BY total_revenue DESC";
            }
            else if (selected.Contains("період + тип + місце"))
            {
                query = @"
                SELECT 
                    f.date_id,
                    et.type_name,
                    v.venue_name,
                    v.city,
                    SUM(f.quantity) AS total_tickets,
                    SUM(f.total_amount) AS total_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.EventType et 
                    ON f.event_type_id = et.event_type_id
                JOIN TicketSales_OLTP.dbo.Venue v 
                    ON f.venue_id = v.venue_id
                GROUP BY 
                    f.date_id,
                    et.type_name,
                    v.venue_name,
                    v.city
                ORDER BY f.date_id";
            }
            else if (selected.Contains("Повернення"))
            {
                query = @"
                SELECT 
                    f.date_id,
                    m.full_name,
                    et.type_name,
                    COUNT(*) AS return_count,
                    SUM(f.total_amount) AS lost_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.Manager m 
                    ON f.manager_id = m.manager_id
                JOIN TicketSales_OLTP.dbo.EventType et 
                    ON f.event_type_id = et.event_type_id
                WHERE f.is_returned = 1
                GROUP BY 
                    f.date_id,
                    m.full_name,
                    et.type_name
                ORDER BY f.date_id";
            }

            // Продажі (3 виміри)
            if (selected.Contains("період + тип + місце"))
            {
                filtersPanel.Visibility = Visibility.Visible;
                cityPanel.Visibility = Visibility.Visible;
                totalText.Visibility = Visibility.Visible;
            }
            // Повернення (3 виміри)
            else if (selected.Contains("Повернення"))
            {
                filtersPanel.Visibility = Visibility.Visible;
                managerPanel.Visibility = Visibility.Visible;
                totalText.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(query))
            {
                LoadAnalytics(query);
            }
        }
        public void LoadAnalyticsOptions()
        {
            analyticsComboBox.Items.Add("Продажі за типами заходів");
            analyticsComboBox.Items.Add("Продажі за подіями");
            analyticsComboBox.Items.Add("Продажі за менеджерами");
            analyticsComboBox.Items.Add("Продажі за місцями");
            analyticsComboBox.Items.Add("Продажі (період + тип + місце)");
            analyticsComboBox.Items.Add("Повернення (період + менеджер + тип)");
        }

        public virtual void LoadFilters()
        {
            yearCombo.Items.Add("Всі");
            yearCombo.Items.Add("2024");
            yearCombo.Items.Add("2025");
            yearCombo.Items.Add("2026");

            monthCombo.Items.Add("Всі");
            for (int i = 1; i <= 12; i++)
                monthCombo.Items.Add(i);

            DataTable types = _db.GetOLTPData("SELECT type_name FROM EventType");
            typeCombo.Items.Add("Всі");
            foreach (DataRow row in types.Rows) typeCombo.Items.Add(row["type_name"].ToString());

            DataTable cities = _db.GetOLTPData("SELECT DISTINCT city FROM Venue");
            cityCombo.Items.Add("Всі");
            foreach (DataRow row in cities.Rows) cityCombo.Items.Add(row["city"].ToString());

            DataTable managers = _db.GetOLTPData("SELECT full_name FROM Manager");
            managerCombo.Items.Add("Всі");
            foreach (DataRow row in managers.Rows) managerCombo.Items.Add(row["full_name"].ToString());
        }
        private void ResetFilters()
        {
            yearCombo.SelectedIndex = 0;
            monthCombo.SelectedIndex = 0;
            typeCombo.SelectedIndex = 0;
            cityCombo.SelectedIndex = 0;
            managerCombo.SelectedIndex = 0;
        }
        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (analyticsComboBox.SelectedItem == null)
                return;

            string selected = analyticsComboBox.SelectedItem.ToString();
            string query = "";
            string groupBy = "";
            string selectDate = "";

            if (selected.Contains("період + тип + місце"))
            {
                // визначаємо рівень
                if (yearCombo.Text == "Всі")
                {
                    selectDate = "(f.date_id / 10000) AS year";
                    groupBy = "(f.date_id / 10000)";
                }
                else if (monthCombo.Text == "Всі")
                {
                    selectDate = @"
                    DATENAME(MONTH, DATEFROMPARTS(
                        f.date_id / 10000,
                        (f.date_id / 100) % 100,
                        1
                    )) AS month";

                    groupBy = @"
                    DATENAME(MONTH, DATEFROMPARTS(
                        f.date_id / 10000,
                        (f.date_id / 100) % 100,
                        1
                    ))";
                }
                else
                {
                    selectDate = "(f.date_id % 100) AS day";
                    groupBy = "(f.date_id % 100)";
                }

                query = $@"
                SELECT 
                    {selectDate},
                    SUM(f.quantity) AS total_tickets,
                    SUM(f.total_amount) AS total_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.EventType et 
                    ON f.event_type_id = et.event_type_id
                JOIN TicketSales_OLTP.dbo.Venue v 
                    ON f.venue_id = v.venue_id
                WHERE 1=1
                ";

                if (yearCombo.SelectedItem != null && yearCombo.SelectedItem.ToString() != "Всі")
                {
                    query += $" AND (f.date_id / 10000) = {yearCombo.SelectedItem}";
                }

                if (monthCombo.SelectedItem != null && monthCombo.SelectedItem.ToString() != "Всі")
                {
                    query += $" AND ((f.date_id / 100) % 100) = {monthCombo.SelectedItem}";
                }

                if (typeCombo.SelectedItem != null && typeCombo.SelectedItem.ToString() != "Всі")
                {
                    query += $" AND et.type_name = N'{typeCombo.SelectedItem}'";
                }

                if (cityCombo.SelectedItem != null && cityCombo.SelectedItem.ToString() != "Всі")
                {
                    query += $" AND v.city = N'{cityCombo.SelectedItem}'";
                }


                if (monthCombo.Text == "Всі" && yearCombo.Text != "Всі")
                {
                    query += $@"
                    GROUP BY {groupBy}, ((f.date_id / 100) % 100)
                    ORDER BY ((f.date_id / 100) % 100)";
                }
                else
                {
                    query += $" GROUP BY {groupBy} ORDER BY {groupBy}";
                }

                // тотал
                string totalQuery = @"
                SELECT 
                    SUM(f.quantity) AS total_tickets,
                    SUM(f.total_amount) AS total_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.EventType et 
                    ON f.event_type_id = et.event_type_id
                JOIN TicketSales_OLTP.dbo.Venue v 
                    ON f.venue_id = v.venue_id
                WHERE 1=1
                ";

                if (!string.IsNullOrWhiteSpace(yearCombo.Text) && yearCombo.Text != "Всі")
                    totalQuery += $" AND (f.date_id / 10000) = {yearCombo.Text}";

                if (!string.IsNullOrWhiteSpace(monthCombo.Text) && monthCombo.Text != "Всі")
                    totalQuery += $" AND ((f.date_id / 100) % 100) = {monthCombo.Text}";

                if (!string.IsNullOrWhiteSpace(typeCombo.Text) && typeCombo.Text != "Всі")
                    totalQuery += $" AND et.type_name = N'{typeCombo.Text}'";

                if (!string.IsNullOrWhiteSpace(cityCombo.Text) && cityCombo.Text != "Всі")
                    totalQuery += $" AND v.city = N'{cityCombo.Text}'";

                LoadAnalytics(query);

                using var conn = _db.GetDWHConnection();
                conn.Open();

                SqlCommand cmd = new SqlCommand(totalQuery, conn);
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var tickets = reader["total_tickets"] != DBNull.Value ? reader["total_tickets"] : 0;
                    var money = reader["total_revenue"] != DBNull.Value ? reader["total_revenue"] : 0;

                    totalText.Text = $"Загалом: {tickets} квитків, {money} грн";
                }

                reader.Close();
            }
            else if (selected.Contains("Повернення"))
            {
                if (yearCombo.Text == "Всі")
                {
                    selectDate = "(f.date_id / 10000) AS year";
                    groupBy = "(f.date_id / 10000)";
                }
                else if (monthCombo.Text == "Всі")
                {
                    selectDate = @"
                    DATENAME(MONTH, DATEFROMPARTS(
                        f.date_id / 10000,
                        (f.date_id / 100) % 100,
                        1
                    )) AS month";

                    groupBy = @"
                    DATENAME(MONTH, DATEFROMPARTS(
                        f.date_id / 10000,
                        (f.date_id / 100) % 100,
                        1
                    ))";
                }
                else
                {
                    selectDate = "(f.date_id % 100) AS day";
                    groupBy = "(f.date_id % 100)";
                }

                query = $@"
                SELECT 
                    {selectDate},
                    COUNT(*) AS return_count,
                    SUM(f.total_amount) AS lost_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.Manager m 
                    ON f.manager_id = m.manager_id
                JOIN TicketSales_OLTP.dbo.EventType et 
                    ON f.event_type_id = et.event_type_id
                WHERE f.is_returned = 1
                ";

                if (yearCombo.SelectedItem != null && yearCombo.SelectedItem.ToString() != "Всі")
                    query += $" AND (f.date_id / 10000) = {yearCombo.SelectedItem}";

                if (monthCombo.SelectedItem != null && monthCombo.SelectedItem.ToString() != "Всі")
                    query += $" AND ((f.date_id / 100) % 100) = {monthCombo.SelectedItem}";

                if (typeCombo.SelectedItem != null && typeCombo.SelectedItem.ToString() != "Всі")
                    query += $" AND et.type_name = N'{typeCombo.SelectedItem}'";

                if (managerCombo.SelectedItem != null && managerCombo.SelectedItem.ToString() != "Всі")
                    query += $" AND m.full_name = N'{managerCombo.SelectedItem}'";
                if (monthCombo.Text == "Всі" && yearCombo.Text != "Всі")
                {
                    query += $@"
                    GROUP BY {groupBy}, ((f.date_id / 100) % 100)
                    ORDER BY ((f.date_id / 100) % 100)";
                }
                else
                {
                    query += $" GROUP BY {groupBy} ORDER BY {groupBy}";
                }

                // тотал
                string totalQuery = @"
                SELECT COUNT(*) AS return_count,
                SUM(f.total_amount) AS lost_revenue
                FROM FactTicketSale f
                JOIN TicketSales_OLTP.dbo.Manager m 
                    ON f.manager_id = m.manager_id
                JOIN TicketSales_OLTP.dbo.EventType et 
                    ON f.event_type_id = et.event_type_id
                WHERE f.is_returned = 1
                ";

                if (!string.IsNullOrWhiteSpace(yearCombo.Text) && yearCombo.Text != "Всі")
                    totalQuery += $" AND (f.date_id / 10000) = {yearCombo.Text}";

                if (!string.IsNullOrWhiteSpace(monthCombo.Text) && monthCombo.Text != "Всі")
                    totalQuery += $" AND ((f.date_id / 100) % 100) = {monthCombo.Text}";

                if (!string.IsNullOrWhiteSpace(typeCombo.Text) && typeCombo.Text != "Всі")
                    totalQuery += $" AND et.type_name = N'{typeCombo.Text}'";

                if (!string.IsNullOrWhiteSpace(managerCombo.Text) && managerCombo.Text != "Всі")
                    totalQuery += $" AND m.full_name = N'{managerCombo.Text}'";

                // виконання
                LoadAnalytics(query);

                using var conn = _db.GetDWHConnection();
                conn.Open();

                SqlCommand cmd = new SqlCommand(totalQuery, conn);
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var returns = reader["return_count"] != DBNull.Value ? reader["return_count"] : 0;
                    var lost = reader["lost_revenue"] != DBNull.Value ? reader["lost_revenue"] : 0;

                    totalText.Text = $"Повернення: {returns} | Втрачено: {lost} грн";
                }

                reader.Close();
            }

            if (!string.IsNullOrEmpty(query))
                LoadAnalytics(query);
            
        }
        private void ChartSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Перевірка, чи всі UI-елементи вже завантажились
            if (chartSelector == null || chartSelector.SelectedItem == null || chartYearFilter == null || chartYearFilter.SelectedItem == null)
                return;

            string selectedChart = (chartSelector.SelectedItem as ComboBoxItem).Content.ToString();
            string selectedYear = (chartYearFilter.SelectedItem as ComboBoxItem).Content.ToString();

            // Ховаємо графіки перед малюванням нового
            cartesianChart.Visibility = Visibility.Collapsed;
            pieChart.Visibility = Visibility.Collapsed;

            // Формуємо умову фільтрації за роком
            string yearFilterSql = "";
            if (selectedYear != "Всі роки")
            {
                yearFilterSql = $"WHERE (f.date_id / 10000) = {selectedYear}";
            }

            switch (selectedChart)
            {
                case "Динаміка продажів у часі (Лінійна)":
                    string q1 = $@"
                        SELECT CAST(f.date_id AS varchar) as Label, SUM(f.total_amount) as Value 
                        FROM FactTicketSale f 
                        {yearFilterSql} 
                        GROUP BY f.date_id 
                        ORDER BY f.date_id";
                    var dt1 = _db.GetData(q1);
                    var labels1 = dt1.AsEnumerable().Select(r => FormatDate(r["Label"].ToString())).ToList();
                    var values1 = dt1.AsEnumerable().Select(r => Convert.ToDecimal(r["Value"])).ToList();

                    BuildCartesianChart(new LineSeries { Title = "Виручка (грн)", Values = new ChartValues<decimal>(values1) }, labels1, "Дата");
                    break;

                case "Виручка за місяцями (Гістограма)":
                    string q2 = $@"
                        SELECT (f.date_id/100)%100 as Label, SUM(f.total_amount) as Value 
                        FROM FactTicketSale f 
                        {yearFilterSql} 
                        GROUP BY (f.date_id/100)%100 
                        ORDER BY (f.date_id/100)%100";
                    var dt2 = _db.GetData(q2);
                    var labels2 = dt2.AsEnumerable().Select(r => GetMonthName(Convert.ToInt32(r["Label"]))).ToList();
                    var values2 = dt2.AsEnumerable().Select(r => Convert.ToDecimal(r["Value"])).ToList();

                    BuildCartesianChart(new ColumnSeries { Title = "Виручка (грн)", Values = new ChartValues<decimal>(values2) }, labels2, "Місяць");
                    break;

                case "Частка продажів за типами заходів (Кругова)":
                    string q3 = $@"
                        SELECT et.type_name as Label, SUM(f.total_amount) as Value 
                        FROM FactTicketSale f 
                        JOIN TicketSales_OLTP.dbo.EventType et ON f.event_type_id = et.event_type_id 
                        {yearFilterSql} 
                        GROUP BY et.type_name";
                    BuildPieChart(q3);
                    break;

                case "Продажі по менеджерах (Стовпчикова)":
                    string q4 = $@"
                        SELECT m.full_name as Label, SUM(f.total_amount) as Value 
                        FROM FactTicketSale f 
                        JOIN TicketSales_OLTP.dbo.Manager m ON f.manager_id = m.manager_id 
                        {yearFilterSql} 
                        GROUP BY m.full_name 
                        ORDER BY Value DESC";
                    var dt4 = _db.GetData(q4);
                    var labels4 = dt4.AsEnumerable().Select(r => r["Label"].ToString()).ToList();
                    var values4 = dt4.AsEnumerable().Select(r => Convert.ToDecimal(r["Value"])).ToList();

                    BuildCartesianChart(new ColumnSeries { Title = "Виручка (грн)", Values = new ChartValues<decimal>(values4) }, labels4, "Менеджер");
                    break;

                case "Частка повернених квитків (Кругова)":
                    string q5 = $@"
                        SELECT CASE WHEN f.is_returned=1 THEN 'Повернено' ELSE 'Продано' END as Label, SUM(f.quantity) as Value 
                        FROM FactTicketSale f 
                        {yearFilterSql} 
                        GROUP BY f.is_returned";
                    BuildPieChart(q5);
                    break;

                case "Популярність заходів (Стовпчикова)":
                    string q6 = $@"
                        SELECT e.event_name as Label, SUM(f.quantity) as Value 
                        FROM FactTicketSale f 
                        JOIN TicketSales_OLTP.dbo.Event e ON f.event_id = e.event_id 
                        {yearFilterSql} 
                        GROUP BY e.event_name 
                        ORDER BY Value DESC";
                    var dt6 = _db.GetData(q6);
                    var labels6 = dt6.AsEnumerable().Select(r => r["Label"].ToString()).ToList();
                    var values6 = dt6.AsEnumerable().Select(r => Convert.ToDecimal(r["Value"])).ToList();

                    BuildCartesianChart(new ColumnSeries { Title = "Продано квитків (шт)", Values = new ChartValues<decimal>(values6) }, labels6, "Захід");
                    break;

                case "Топ міст за виручкою (Горизонтальна)":
                    string q7 = $@"
                        SELECT v.city as Label, SUM(f.total_amount) as Value 
                        FROM FactTicketSale f 
                        JOIN TicketSales_OLTP.dbo.Venue v ON f.venue_id = v.venue_id 
                        {yearFilterSql} 
                        GROUP BY v.city 
                        ORDER BY Value ASC";
                    var dt7 = _db.GetData(q7);
                    var labels7 = dt7.AsEnumerable().Select(r => r["Label"].ToString()).ToList();
                    var values7 = dt7.AsEnumerable().Select(r => Convert.ToDecimal(r["Value"])).ToList();

                    // передаємо true в кінці, щоб метод знав, що це горизонтальний графік
                    BuildCartesianChart(new RowSeries { Title = "Виручка (грн)", Values = new ChartValues<decimal>(values7) }, labels7, "Місто", true);
                    break;
            }
        }
        private void BuildCartesianChart(Series series, List<string> labels, string categoryTitle, bool isHorizontal = false)
        {
            cartesianChart.Series = new SeriesCollection { series };

            // очищаємо підписи на обох осях перед оновленням
            sharedAxisX.Labels = null;
            sharedAxisX.Title = "";
            sharedAxisY.Labels = null;
            sharedAxisY.Title = "";

            if (isHorizontal)
            {
                sharedAxisY.Labels = labels;
                sharedAxisY.Title = categoryTitle;
                sharedAxisX.Title = "Показник";
            }
            else
            {
                sharedAxisX.Labels = labels;
                sharedAxisX.Title = categoryTitle;
                sharedAxisY.Title = "Показник";
            }

            cartesianChart.Visibility = Visibility.Visible;
        }

        private void BuildPieChart(string query)
        {
            var data = _db.GetData(query);
            pieChart.Series = new SeriesCollection();

            foreach (DataRow row in data.Rows)
            {
                pieChart.Series.Add(new PieSeries
                {
                    Title = row["Label"].ToString(),
                    Values = new ChartValues<decimal> { Convert.ToDecimal(row["Value"]) },
                    DataLabels = true
                });
            }
            pieChart.Visibility = Visibility.Visible;
        }
        private string FormatDate(string dateId)
        {
            // Перетворює 20231015 на 15.10.2023
            if (dateId.Length == 8)
                return $"{dateId.Substring(6, 2)}.{dateId.Substring(4, 2)}.{dateId.Substring(0, 4)}";
            return dateId;
        }

        private string GetMonthName(int monthNum)
        {
            string[] months = { "", "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень", "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень" };
            if (monthNum >= 1 && monthNum <= 12) return months[monthNum];
            return monthNum.ToString();
        }

        private void SelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Зображення (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|Всі файли (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.EndInit();

                    AuthorPhotoBrush.ImageSource = bitmap;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Помилка завантаження фото: " + ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // Віртуальні обгортки для Moq
        public virtual void OpenConnection(SqlConnection conn) => conn.Open();
        public virtual void ExecuteCommand(SqlCommand cmd) => cmd.ExecuteNonQuery();
        public virtual void ShowMessage(string message) => MessageBox.Show(message);
        public virtual MessageBoxResult ShowConfirmMessage(string message, string title) => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        public virtual void OpenAddSaleWindow() { new AddSaleWindow().ShowDialog(); }
        public virtual void OpenEditSaleWindow(int id) { new EditSaleWindow(id).ShowDialog(); }
        public virtual void RunEtlTransfer() { new EtlService(new DatabaseHelper()).TransferData(); }
        public virtual IDataReader GetReader(SqlCommand cmd) => cmd.ExecuteReader();
    }
}