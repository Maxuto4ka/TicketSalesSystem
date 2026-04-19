using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TicketSalesSystem.Database;
using static TicketSalesSystem.Database.DatabaseHelper;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TicketSalesSystem.Tests")]

namespace TicketSalesSystem
{
    public partial class EditSaleWindow : Window
    {
        private readonly IDatabaseHelper _db = new DatabaseHelper();
        private int saleId;
        public EditSaleWindow(int saleId)
        {
            _db = new DatabaseHelper();
            this.saleId = saleId;
            InitializeComponent();
            LoadComboBoxes();
            LoadSaleData();
        }
        // Конструктор для тестів
        public EditSaleWindow(int saleId, IDatabaseHelper db)
        {
            _db = db;
            this.saleId = saleId;

            EventBox = new ComboBox();
            ManagerBox = new ComboBox();
            ClientBox = new ComboBox();
            QuantityBox = new TextBox();
            RowBox = new TextBox();
            SeatBox = new TextBox();
            TotalLabel = new TextBlock();
        }
        public virtual void LoadComboBoxes()
        {
            DataTable eventTable = _db.GetOLTPData("SELECT event_id, event_name, base_price FROM Event");
            EventBox.ItemsSource = eventTable.DefaultView;
            EventBox.DisplayMemberPath = "event_name";
            EventBox.SelectedValuePath = "event_id";

            DataTable managerTable = _db.GetOLTPData("SELECT manager_id, full_name FROM Manager");
            ManagerBox.ItemsSource = managerTable.DefaultView;
            ManagerBox.DisplayMemberPath = "full_name";
            ManagerBox.SelectedValuePath = "manager_id";

            DataTable clientTable = _db.GetOLTPData("SELECT client_id, full_name FROM Client");
            ClientBox.ItemsSource = clientTable.DefaultView;
            ClientBox.DisplayMemberPath = "full_name";
            ClientBox.SelectedValuePath = "client_id";
        }

        public virtual void LoadSaleData()
        {
            using var conn = _db.GetOLTPConnection();
            OpenConnection(conn);

            SqlCommand cmd = new SqlCommand(@"
            SELECT s.event_id, s.manager_id, s.client_id,
            s.row_number, s.seat_number, s.quantity, s.price
            FROM TicketSale s
            WHERE s.sale_id=@id", conn);
            cmd.Parameters.AddWithValue("@id", saleId);

            using var reader = GetReader(cmd);
            if (reader.Read())
            {
                EventBox.SelectedValue = reader["event_id"];
                ManagerBox.SelectedValue = reader["manager_id"];
                ClientBox.SelectedValue = reader["client_id"];
                RowBox.Text = reader["row_number"].ToString();
                SeatBox.Text = reader["seat_number"].ToString();
                QuantityBox.Text = reader["quantity"].ToString();

                decimal price = Convert.ToDecimal(reader["price"]);
                int quantity = Convert.ToInt32(reader["quantity"]);
                TotalLabel.Text = (price * quantity).ToString("0.00");
            }
        }

        public void QuantityBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (EventBox.SelectedValue == null || string.IsNullOrEmpty(QuantityBox.Text))
                return;
            using var conn = _db.GetOLTPConnection();
            OpenConnection(conn);

            int eventId = (int)EventBox.SelectedValue;

            SqlCommand cmd = new SqlCommand("SELECT base_price FROM Event WHERE event_id=@id", conn);
            cmd.Parameters.AddWithValue("@id", eventId);

            decimal price = GetScalarValue(cmd);

            if (int.TryParse(QuantityBox.Text, out int q))
            {
                decimal total = q * price;
                TotalLabel.Text = total.ToString("0.00");
            }
        }

        public void EventBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // при зміні Event, оновлюємо Total
            QuantityBox_TextChanged(null, null);
        }

        public void UpdateSale_Click(object sender, RoutedEventArgs e)
        {

            int eventId = (int)EventBox.SelectedValue;
            int managerId = (int)ManagerBox.SelectedValue;
            int clientId = (int)ClientBox.SelectedValue;
            int quantity = int.Parse(QuantityBox.Text);
            int row = int.Parse(RowBox.Text);
            int seat = int.Parse(SeatBox.Text);
            ExecuteUpdate(eventId, managerId, clientId, quantity, row, seat);

            ShowSuccessMessage("Sale updated successfully");
            CloseWindow();
        }

        public virtual void ExecuteUpdate(int eventId, int managerId, int clientId, int quantity, int row, int seat)
        {
            using var conn = _db.GetOLTPConnection();
            OpenConnection(conn);

            SqlCommand priceCmd = new SqlCommand("SELECT base_price FROM Event WHERE event_id=@id", conn);
            priceCmd.Parameters.AddWithValue("@id", eventId);
            decimal price = GetScalarValue(priceCmd);
            decimal total = price * quantity;

            SqlCommand update = new SqlCommand(@"
            UPDATE TicketSale
            SET event_id=@event, manager_id=@manager, client_id=@client,
            row_number=@row, seat_number=@seat, quantity=@q,
            price=@price, total_amount=@total
            WHERE sale_id=@id", conn);

            update.Parameters.AddWithValue("@event", eventId);
            update.Parameters.AddWithValue("@manager", managerId);
            update.Parameters.AddWithValue("@client", clientId);
            update.Parameters.AddWithValue("@row", row);
            update.Parameters.AddWithValue("@seat", seat);
            update.Parameters.AddWithValue("@q", quantity);
            update.Parameters.AddWithValue("@price", price);
            update.Parameters.AddWithValue("@total", total);
            update.Parameters.AddWithValue("@id", saleId);

            ExecuteCommand(update);
        }

        // Віртуальні обгортки
        public virtual void OpenConnection(SqlConnection conn) => conn.Open();
        public virtual decimal GetScalarValue(SqlCommand cmd) => (decimal)cmd.ExecuteScalar();
        public virtual void ExecuteCommand(SqlCommand cmd) => cmd.ExecuteNonQuery();
        public virtual IDataReader GetReader(SqlCommand cmd) => cmd.ExecuteReader();
        public virtual void ShowSuccessMessage(string message) => MessageBox.Show(message);
        public virtual void CloseWindow() => Close();
    }
}
