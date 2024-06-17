using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WebApplication4.Controllers
{
    [ApiController]
    [Route("api/location")]
    public class LocationController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LocationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        //page2 - 1st graph
        [HttpPost("get-branch-and-employee-data-day-wise")]
        public IActionResult GetBranchAndEmployeeData([FromBody] CombinedRequest request)
        {
            List<string> branches = new List<string>();
            List<DailyAccessRecord> records = new List<DailyAccessRecord>();

            try
            {
                using (SqlConnection connection = CreateSqlConnection())
                {
                    connection.Open();

                    // Fetch branch locations
                    SqlCommand branchCommand = connection.CreateCommand();
                    branchCommand.CommandText = @"
                        SELECT DISTINCT tadesc FROM IF_IF_areal;";

                    using (SqlDataReader branchReader = branchCommand.ExecuteReader())
                    {
                        while (branchReader.Read())
                        {
                            branches.Add(branchReader["tadesc"].ToString());
                        }
                    }

                    // If a branch is provided, fetch employee access records
                    if (!string.IsNullOrWhiteSpace(request.Branch))
                    {
                        // Determine the date range using the helper function
                        var (startDate, endDate) = get_start_and_end_date(request.DateRange);

                        // Check if startDate and endDate are valid
                        if (startDate.HasValue && endDate.HasValue)
                        {
                            // Fetch employee access records
                            SqlCommand accessCommand = connection.CreateCommand();
                            accessCommand.CommandText = @"
                            SELECT COUNT(DISTINCT stssnr) AS 'Total Employee', CAST(zudate AS DATE) AS 'date'
                            FROM IF_IF_accessarchive
                            JOIN IF_IF_tdf ON IF_IF_tdf.tddesc = IF_IF_accessarchive.zrtddesc
                            JOIN IF_Person ON IF_Person.stpersnr = IF_IF_accessarchive.zrstpersnr
                            JOIN IF_IF_arealterminals ON IF_IF_arealterminals.TerminalDOR = IF_IF_tdf.Guid
                            JOIN IF_IF_areal ON IF_IF_arealterminals.ArealDOR = IF_IF_areal.Guid
                            WHERE CAST(zudate AS DATE) BETWEEN @startDate AND @endDate
                              AND zuevent = 0 
                              AND tadesc = @branch
                            GROUP BY CAST(zudate AS DATE)
                            ORDER BY CAST(zudate AS DATE)";

                            accessCommand.Parameters.AddWithValue("@startDate", startDate.Value);
                            accessCommand.Parameters.AddWithValue("@endDate", endDate.Value);
                            accessCommand.Parameters.AddWithValue("@branch", request.Branch);

                            using (SqlDataReader accessReader = accessCommand.ExecuteReader())
                            {
                                while (accessReader.Read())
                                {
                                    var record = new DailyAccessRecord
                                    {
                                        Date = accessReader.GetDateTime(accessReader.GetOrdinal("date")),
                                        TotalEmployee = accessReader.GetInt32(accessReader.GetOrdinal("Total Employee"))
                                    };
                                    records.Add(record);
                                }
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"SQL Error: {sqlEx.Message}");
                return StatusCode(500, "A database error occurred while processing your request.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "An error occurred while processing your request.");
            }

            return Ok(new { statuscode = 200, branches = branches, accessRecords = records });
        }

        //Page 2 - 2nd graph
        [HttpPost("get-hour-wise-employee-count")]
        public IActionResult GetLocationWiseEmployeeCount()
        {
            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

            try
            {
                using (SqlConnection connection = CreateSqlConnection())
                {
                    connection.Open();
                    Console.WriteLine("Connection successful");

                    SqlCommand command = connection.CreateCommand();
                    command.CommandText = @"
                    SELECT COUNT(DISTINCT stssnr) as EmpCount,
                     FORMAT(CONVERT(datetime, CONVERT(varchar(10), GETDATE(), 120) + ' ' + RIGHT('0' + CAST(HourOfDay AS varchar(2)), 2) + ':00:00'), 'yyyy-MM-dd HH:mm:ss') as Hour
                     FROM (
                         SELECT stssnr, stname, stvorname, DATEPART(HOUR, zudate) AS HourOfDay,
                                ROW_NUMBER() OVER(PARTITION BY stssnr ORDER BY zudate) AS RowNum
                         FROM IF_IF_accessarchive
                         INNER JOIN IF_IF_tdf ON IF_IF_tdf.tddesc = IF_IF_accessarchive.zrtddesc
                         INNER JOIN IF_Person ON IF_Person.stpersnr = IF_IF_accessarchive.zrstpersnr
                         FULL JOIN IF_IF_arealterminals ON IF_IF_tdf.Guid = IF_IF_arealterminals.TerminalDOR
                         FULL JOIN IF_IF_areal ON IF_IF_arealterminals.ArealDOR = IF_IF_areal.Guid
                         WHERE CAST(zudate AS DATE) = CAST(GETDATE() AS DATE)
                               AND tadesc = 'Electronic Industrial Estate' and zuevent=0 and TRY_CAST(IF_Person.stpersnr as INT) is not null
                     ) AS SubQuery
                     WHERE RowNum = 1 
                     GROUP BY HourOfDay";

                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var record = new Dictionary<string, object>
                    {
                        { "hour", reader["Hour"] },
                        { "EmpCount", reader["EmpCount"] }
                    };
                            results.Add(record);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No data found.");
                    }
                    reader.Close();
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"SQL Error: {sqlEx.Message}");
                return StatusCode(500, "A database error occurred while processing your request.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, "An error occurred while processing your request.");
            }

            if (results.Count > 0)
            {
                return Ok(new { statuscode = 200, data = results });
            }
            else
            {
                return NotFound("No data found.");
            }
        }

        ////page 2nd- 3rd graph
        //[HttpPost("get-month-wise-employee-count")]
        //public IActionResult GetMonthWiseEmployeeData([FromBody] CombinedRequest request)
        //{
        //    List<string> branches = new List<string>();
        //}

        private (DateTime? startDate, DateTime? endDate) get_start_and_end_date(string daterange)
        {
            try
            {
                DateTime? startDate = null;
                DateTime? endDate = null;

                DateTime current_time = DateTime.UtcNow;

                switch (daterange.ToLower())
                {
                    case "past week":
                        startDate = current_time.AddDays(-7);
                        endDate = current_time;
                        break;

                    case "past 3 weeks":
                        startDate = current_time.AddDays(-21); // 3 weeks = 21 days
                        endDate = current_time;
                        break;

                    case "past month":
                        startDate = current_time.AddMonths(-1);
                        endDate = current_time;
                        break;

                    case "past quarter":
                        startDate = current_time.AddMonths(-3);
                        endDate = current_time;
                        break;

                    default:
                        throw new ArgumentException("Invalid daterange");
                }

                return (startDate, endDate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (null, null);
            }
        }

        public class CombinedRequest
        {
            public string Branch { get; set; }
            public string DateRange { get; set; }
        }

        // Record class for access data
        public class DailyAccessRecord
        {
            public DateTime Date { get; set; }
            public int TotalEmployee { get; set; }
        }

        private SqlConnection CreateSqlConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            return new SqlConnection(connectionString);
        }
    }
}
