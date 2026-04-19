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
    public partial class AddSaleWindow : Window
    {
        private readonly IDatabaseHelper _db; 
        public AddSaleWindow()
        {
            _db = new DatabaseHelper();
            InitializeComponent();
            LoadComboBoxes();
        }

        // Конструктор для тестів
        public AddSaleWindow(IDatabaseHelper db)
        {
            _db = db;
            EventBox = new ComboBox();
            ManagerBox = new ComboBox();
            ClientBox = new ComboBox();
            QuantityBox = new TextBox();
            RowBox = new TextBox();
            SeatBox = new TextBox();
            TotalLabel = new TextBlock();
        }
        public void LoadComboBoxes()
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
        public void QuantityBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EventBox.SelectedValue == null || QuantityBox.Text == "")
                return;

            using var conn = _db.GetOLTPConnection();
            OpenConnection(conn);

            int eventId = (int)EventBox.SelectedValue;

            SqlCommand cmd = new SqlCommand(
                "SELECT base_price FROM Event WHERE event_id=@id", conn);

            cmd.Parameters.AddWithValue("@id", eventId);

            decimal price = GetScalarValue(cmd);

            if (int.TryParse(QuantityBox.Text, out int q))
            {
                decimal total = q * price;
                TotalLabel.Text = total.ToString("0.00");
            }
        }

        public void AddSale_Click(object sender, RoutedEventArgs e)
        {
            int eventId = (int)EventBox.SelectedValue;
            int managerId = (int)ManagerBox.SelectedValue;
            int clientId = (int)ClientBox.SelectedValue;

            int quantity = int.Parse(QuantityBox.Text);
            int row = int.Parse(RowBox.Text);
            int seat = int.Parse(SeatBox.Text);

            ExecuteSale(eventId, managerId, clientId, quantity, row, seat);

            ShowSuccessMessage("Sale added successfully");
            CloseWindow();
        }

        public virtual void ExecuteSale(int eventId, int managerId, int clientId, int quantity, int row, int seat)
        {
            using var conn = _db.GetOLTPConnection();
            OpenConnection(conn);

            SqlCommand priceCmd = new SqlCommand("SELECT base_price FROM Event WHERE event_id=@id", conn);
            priceCmd.Parameters.AddWithValue("@id", eventId);

            decimal price = GetScalarValue(priceCmd);
            decimal total = price * quantity;

            SqlCommand insert = new SqlCommand(@"
            INSERT INTO TicketSale
            (event_id,manager_id,client_id,sale_datetime,row_number,seat_number,quantity,price,total_amount,is_returned)
            VALUES
            (@event,@manager,@client,GETDATE(),@row,@seat,@q,@price,@total,0)", conn);

            insert.Parameters.AddWithValue("@event", eventId);
            insert.Parameters.AddWithValue("@manager", managerId);
            insert.Parameters.AddWithValue("@client", clientId);
            insert.Parameters.AddWithValue("@row", row);
            insert.Parameters.AddWithValue("@seat", seat);
            insert.Parameters.AddWithValue("@q", quantity);
            insert.Parameters.AddWithValue("@price", price);
            insert.Parameters.AddWithValue("@total", total);

            ExecuteCommand(insert);
        }

        // Віртуальні обгортки для Moq
        public virtual void OpenConnection(SqlConnection conn) => conn.Open();
        public virtual decimal GetScalarValue(SqlCommand cmd) => (decimal)cmd.ExecuteScalar();
        public virtual void ExecuteCommand(SqlCommand cmd) => cmd.ExecuteNonQuery();
        public virtual void ShowSuccessMessage(string message) => MessageBox.Show(message);
        public virtual void CloseWindow() => Close();
    }
}
