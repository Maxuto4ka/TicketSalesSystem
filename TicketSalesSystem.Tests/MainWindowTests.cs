using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;
using static TicketSalesSystem.Database.DatabaseHelper;

namespace TicketSalesSystem.Tests
{
    public class MainWindowTests
    {
        private void RunInSTA(Action action)
        {
            Exception exception = null;
            Thread thread = new Thread(() => { try { action(); } catch (Exception ex) { exception = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }

        [Fact]
        public void LoadFilters_FillsComboBoxes_FromDatabase()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();

                var dummyTable = new DataTable();
                dummyTable.Columns.Add("type_name");
                dummyTable.Columns.Add("city");
                dummyTable.Columns.Add("full_name");

                dummyTable.Rows.Add("TestType", "TestCity", "TestManager");

                dbMock.Setup(db => db.GetOLTPData(It.IsAny<string>())).Returns(dummyTable);

                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                // Act
                windowMock.Object.LoadFilters();

                // Assert
                Assert.True(windowMock.Object.typeCombo.Items.Contains("Всі"));
                Assert.True(windowMock.Object.typeCombo.Items.Contains("TestType"));

                dbMock.Verify(db => db.GetOLTPData(It.IsAny<string>()), Times.Exactly(3));
            });
        }

        [Fact]
        public void LoadSales_SetsGridItemsSource()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var dt = new DataTable();
                dbMock.Setup(db => db.GetOLTPData(It.IsAny<string>())).Returns(dt);

                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                // Act
                windowMock.Object.LoadSales();

                // Assert
                Assert.NotNull(windowMock.Object.salesGrid.ItemsSource);
                dbMock.Verify(db => db.GetOLTPData(It.IsRegex("TicketSale")), Times.Once);
            });
        }

        [Fact]
        public void Transfer_Click_ExecutesEtlAndShowsMessage()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                windowMock.Setup(w => w.RunEtlTransfer());
                windowMock.Setup(w => w.ShowMessage(It.IsAny<string>()));

                // Act
                windowMock.Object.Transfer_Click(null, null);

                // Assert
                windowMock.Verify(w => w.RunEtlTransfer(), Times.Once);
                windowMock.Verify(w => w.ShowMessage("Data transferred to warehouse"), Times.Once);
            });
        }

        [Fact]
        public void DeleteSale_Click_DoesNothing_WhenUserCancels()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                var dt = new DataTable();
                dt.Columns.Add("sale_id", typeof(int));
                dt.Columns.Add("event_name", typeof(string));
                dt.Columns.Add("manager", typeof(string));
                dt.Columns.Add("client", typeof(string));
                dt.Columns.Add("total_amount", typeof(string));
                dt.Rows.Add(1, "Concert", "John", "Doe", "500");

                windowMock.Object.salesGrid.ItemsSource = dt.DefaultView;
                windowMock.Object.salesGrid.SelectedIndex = 0;

                windowMock.Setup(w => w.ShowConfirmMessage(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(MessageBoxResult.No);

                // Act
                windowMock.Object.DeleteSale_Click(null, null);

                // Assert
                windowMock.Verify(w => w.ExecuteCommand(It.IsAny<SqlCommand>()), Times.Never);
            });
        }
        [Fact]
        public void DeleteSale_Click_ExecutesCommand_WhenUserConfirms()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                var dt = new DataTable();
                dt.Columns.Add("sale_id", typeof(int));
                dt.Columns.Add("event_name", typeof(string));
                dt.Columns.Add("manager", typeof(string));
                dt.Columns.Add("client", typeof(string));
                dt.Columns.Add("total_amount", typeof(string));
                dt.Rows.Add(1, "Concert", "John", "Doe", "500");

                windowMock.Object.salesGrid.ItemsSource = dt.DefaultView;
                windowMock.Object.salesGrid.SelectedIndex = 0;

                windowMock.Setup(w => w.ShowConfirmMessage(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(MessageBoxResult.Yes);

                windowMock.Setup(w => w.OpenConnection(It.IsAny<SqlConnection>()));
                windowMock.Setup(w => w.ExecuteCommand(It.IsAny<SqlCommand>()));
                windowMock.Setup(w => w.ShowMessage(It.IsAny<string>()));
                windowMock.Setup(w => w.LoadSales());

                // Act
                windowMock.Object.DeleteSale_Click(null, null);

                // Assert
                windowMock.Verify(w => w.ExecuteCommand(It.IsAny<SqlCommand>()), Times.Once);
                windowMock.Verify(w => w.ShowMessage("Sale deleted."), Times.Once);
                windowMock.Verify(w => w.LoadSales(), Times.Once);
            });
        }

        [Fact]
        public void AddSale_Click_OpensWindowAndReloadsSales()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                windowMock.Setup(w => w.OpenAddSaleWindow());
                windowMock.Setup(w => w.LoadSales());

                // Act
                windowMock.Object.AddSale_Click(null, null);

                // Assert
                windowMock.Verify(w => w.OpenAddSaleWindow(), Times.Once);
                windowMock.Verify(w => w.LoadSales(), Times.Once);
            });
        }

        [Fact]
        public void AnalyticsCombo_SelectionChanged_GeneratesQuery_ForTypes()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                windowMock.Setup(w => w.LoadAnalytics(It.IsAny<string>()));

                windowMock.Object.analyticsComboBox.SelectedItem = "Продажі за типами заходів";

                // Act
                windowMock.Object.analyticsComboBox_SelectionChanged(null, null);

                // Assert
                windowMock.Verify(w => w.LoadAnalytics(It.IsRegex("et\\.type_name")), Times.Once);
            });
        }

        [Fact]
        public void AnalyticsCombo_SelectionChanged_ShowsFilters_ForReturns()
        {
            RunInSTA(() =>
            {
                // Arrange
                var dbMock = new Mock<IDatabaseHelper>();
                var windowMock = new Mock<MainWindow>(dbMock.Object) { CallBase = true };

                windowMock.Setup(w => w.LoadAnalytics(It.IsAny<string>()));

                windowMock.Object.analyticsComboBox.SelectedItem = "Повернення (період + менеджер + тип)";

                // Act
                windowMock.Object.analyticsComboBox_SelectionChanged(null, null);

                // Assert
                Assert.Equal(Visibility.Visible, windowMock.Object.filtersPanel.Visibility);
                Assert.Equal(Visibility.Visible, windowMock.Object.managerPanel.Visibility);

                windowMock.Verify(w => w.LoadAnalytics(It.IsRegex("is_returned = 1")), Times.Once);
            });
        }
    }
}
