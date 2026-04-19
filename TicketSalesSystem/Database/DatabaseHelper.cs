using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using static TicketSalesSystem.Database.DatabaseHelper;

namespace TicketSalesSystem.Database
{
    public class DatabaseHelper : IDatabaseHelper
    {
        public static string OLTP =
            "Server=localhost;Database=TicketSales_OLTP;Trusted_Connection=True;TrustServerCertificate=True;";

        public static string DWH =
            "Server=localhost;Database=TicketSales_DWH;Trusted_Connection=True;TrustServerCertificate=True;";

        public virtual SqlConnection CreateConnection(string connectionString)
        {
            return new SqlConnection(connectionString);
        }

        public virtual SqlConnection GetOLTPConnection()
        {
            return CreateConnection(OLTP);
        }

        public virtual SqlConnection GetDWHConnection()
        {
            return CreateConnection(DWH);
        }

        public virtual SqlDataAdapter CreateAdapter(string query, SqlConnection conn)
        {
            return new SqlDataAdapter(query, conn);
        }

        public DataTable GetData(string query)
        {
            using var conn = GetDWHConnection();
            conn.Open();

            var adapter = CreateAdapter(query, conn);

            DataTable table = new DataTable();
            adapter.Fill(table);

            return table;
        }
        public DataTable GetOLTPData(string query)
        {
            using var conn = GetOLTPConnection();
            conn.Open();
            var adapter = CreateAdapter(query, conn);
            DataTable table = new DataTable();
            adapter.Fill(table);
            return table;
        }

        public interface IDatabaseHelper
        {
            SqlConnection GetOLTPConnection();
            SqlConnection GetDWHConnection();
            DataTable GetData(string query);
            DataTable GetOLTPData(string query);
        }
    }
}
