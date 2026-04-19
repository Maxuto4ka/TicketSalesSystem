using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using TicketSalesSystem.Database;
using static TicketSalesSystem.Database.DatabaseHelper;


namespace TicketSalesSystem.Services
{
    public class EtlService
    {
        private readonly IDatabaseHelper _db;

        public EtlService(IDatabaseHelper db)
        {
            _db = db;
        }
        public virtual void OpenConnection(SqlConnection conn)
        {
            conn.Open();
        }
        public void TransferData()
        {
            using var oltp = _db.GetOLTPConnection();
            using var dwh = _db.GetDWHConnection();

            OpenConnection(oltp);
            OpenConnection(dwh);

            int lastDate = GetLastDate(dwh);

            var data = GetNewData(oltp, lastDate);

            InsertData(dwh, data);
        }
        public virtual int GetLastDate(SqlConnection dwh)
        {
            var cmd = new SqlCommand(
                "SELECT ISNULL(MAX(date_id),0) FROM FactTicketSale", dwh);

            return Convert.ToInt32(ExecuteScalar(cmd));
        }

        public virtual List<Dictionary<string, object>> GetNewData(SqlConnection oltp, int lastDate)
        {
            var result = new List<Dictionary<string, object>>();

            string query = @"
            SELECT 
            YEAR(s.sale_datetime)*10000 + MONTH(s.sale_datetime)*100 + DAY(s.sale_datetime) AS date_id,
            s.event_id,
            e.event_type_id,
            e.venue_id,
            s.manager_id,
            s.quantity,
            ISNULL(s.price, e.base_price) AS price,
            s.total_amount,
            s.is_returned
            FROM TicketSale s
            JOIN Event e ON s.event_id = e.event_id
            WHERE NOT EXISTS (
                SELECT 1
                FROM TicketSales_DWH.dbo.FactTicketSale f
                WHERE 
                    f.date_id = YEAR(s.sale_datetime)*10000 + MONTH(s.sale_datetime)*100 + DAY(s.sale_datetime)
                    AND f.event_id = s.event_id
                    AND f.manager_id = s.manager_id
                    AND f.quantity = s.quantity
                    AND f.total_amount = s.total_amount
            )";

            var cmd = new SqlCommand(query, oltp);
            cmd.Parameters.AddWithValue("@lastDate", lastDate);

            using var reader = ExecuteReader(cmd);

            while (reader.Read())
            {
                result.Add(new Dictionary<string, object>
                {
                    ["event"] = reader["event_id"],
                    ["type"] = reader["event_type_id"],
                    ["venue"] = reader["venue_id"],
                    ["manager"] = reader["manager_id"],
                    ["date"] = reader["date_id"],
                    ["q"] = reader["quantity"],
                    ["price"] = reader["price"],
                    ["sum"] = reader["total_amount"],
                    ["ret"] = reader["is_returned"]
                });
            }

            return result;
        }

        public virtual void InsertData(SqlConnection dwh, List<Dictionary<string, object>> data)
        {
            foreach (var row in data)
            {
                var cmd = new SqlCommand(
                    @"INSERT INTO FactTicketSale
                    (event_id,event_type_id,venue_id,manager_id,date_id,quantity,price,total_amount,is_returned)
                    VALUES
                    (@event,@type,@venue,@manager,@date,@q,@price,@sum,@ret)", dwh);

                cmd.Parameters.AddWithValue("@event", row["event"]);
                cmd.Parameters.AddWithValue("@type", row["type"]);
                cmd.Parameters.AddWithValue("@venue", row["venue"]);
                cmd.Parameters.AddWithValue("@manager", row["manager"]);
                cmd.Parameters.AddWithValue("@date", row["date"]);
                cmd.Parameters.AddWithValue("@q", row["q"]);
                cmd.Parameters.AddWithValue("@price", row["price"]);
                cmd.Parameters.AddWithValue("@sum", row["sum"]);
                cmd.Parameters.AddWithValue("@ret", row["ret"]);

                ExecuteNonQuery(cmd);
            }
        }
        public virtual object ExecuteScalar(SqlCommand cmd)
        {
            return cmd.ExecuteScalar();
        }

        public virtual IDataReader ExecuteReader(SqlCommand cmd)
        {
            return cmd.ExecuteReader();
        }

        public virtual void ExecuteNonQuery(SqlCommand cmd)
        {
            cmd.ExecuteNonQuery();
        }
    }
}
