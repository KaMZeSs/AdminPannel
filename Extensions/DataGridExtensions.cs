using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AdminPannel.Extensions
{
    public static class DataGridExtensions
    {
        public static DataTable GetDataTable(this DataGrid dataGrid)
        {
            if (dataGrid.Items.Count == 0)
            {
                throw new ArgumentException();
            }

            DataTable dataTable = new DataTable();

            var types = new Dictionary<String, Type>();

            var first_row = dataGrid.Items.Cast<IDictionary<string, object>>().First();
            foreach (var column in dataGrid.Columns)
            {
                var value = first_row?[column.SortMemberPath];
                types.Add(column.SortMemberPath, value?.GetType() ?? typeof(String));
            }

            var counter = 0;
            foreach (DataGridColumn column in dataGrid.Columns)
            {
                if (column.Header is not TextBlock header)
                {
                    dataTable.Columns.Add($"Column {++counter}", types[column.SortMemberPath]);
                    continue;
                }
                dataTable.Columns.Add(header.Text, types[column.SortMemberPath]);
            }

            foreach (var row in dataGrid.Items)
            {
                DataRow data_row = dataTable.NewRow();
                var dictionary = row as IDictionary<string, object>;

                counter = 0;
                foreach (var column in dataGrid.Columns)
                {
                    data_row[counter++] = dictionary?[column.SortMemberPath];
                }

                dataTable.Rows.Add(data_row);
            }

            return dataTable;
        }
    }
}
