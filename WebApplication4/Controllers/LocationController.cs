using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WebApplication4.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public LocationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        //Page 2 - 2nd graph
        [HttpPost("GetLocationWiseEmployeeCount")]
        public IActionResult GetLocationWiseEmployeeCount([FromBody] LocationRequest request)
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
                        SELECT 
                            DATEPART(hour, IF_IF_empcurronl.eclbtimestamp) AS hour,
                            COUNT(*) AS TotalCount
                        FROM IF_IF_empcurronl
                        INNER JOIN IF_IF_raumzone ON IF_IF_empcurronl.AccessZoneDOR = IF_IF_raumzone.Guid
                        WHERE 
                            IF_IF_raumzone.rzrzwonl = 'B' 
                            AND eclbtimestamp >= CAST(GETDATE() AS DATE) 
                            AND IF_IF_raumzone.rznr NOT IN (281, 309, 310) 
                            AND SUBSTRING(IF_IF_raumzone.rzdesc, CHARINDEX('-', IF_IF_raumzone.rzdesc) + 2, LEN(IF_IF_raumzone.rzdesc)) = @Location
                        GROUP BY 
                            IF_IF_empcurronl.AccessZoneDOR, 
                            IF_IF_raumzone.rzdesc, 
                            DATEPART(hour, IF_IF_empcurronl.eclbtimestamp)
                        ORDER BY 
                            IF_IF_raumzone.rzdesc DESC, 
                            DATEPART(hour, IF_IF_empcurronl.eclbtimestamp)";

                    command.Parameters.AddWithValue("@Location", request.Location);

                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var record = new Dictionary<string, object>
                    {
                        { "hour", reader["hour"] },
                        { "TotalCount", reader["TotalCount"] }
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


        [HttpPost("GetBranchAndEmployeeData")]
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
                        SELECT DISTINCT 
                            rzdesc AS LOCATION
                        FROM IF_IF_raumzone
                        WHERE rzrzwonl = 'B' 
                          AND rznr NOT IN (281, 309, 310) 
                          AND rzfeld1 <> ''
                        ORDER BY rzdesc DESC;";

                    SqlDataReader branchReader = branchCommand.ExecuteReader();

                    while (branchReader.Read())
                    {
                        branches.Add(branchReader["LOCATION"].ToString());
                    }
                    branchReader.Close();

                    // If a branch is provided, fetch employee access records
                    if (!string.IsNullOrWhiteSpace(request.Branch))
                    {
                        // Determine the date range using the helper function
                        var (startDate, endDate) = GetDateRange(request.DateRange);

                        // Fetch employee access records
                        SqlCommand accessCommand = connection.CreateCommand();
                        accessCommand.CommandText = @"
                            SELECT count(distinct stssnr) as 'Total Employee',cast(zudate as date) as 'date' from IF_IF_accessarchive
                            join IF_IF_tdf on IF_IF_tdf.tddesc=IF_IF_accessarchive.zrtddesc
                            join IF_Person on IF_Person.stpersnr=IF_IF_accessarchive.zrstpersnr
                            join IF_IF_arealterminals on IF_IF_arealterminals.TerminalDOR=IF_IF_tdf.Guid
                            join IF_IF_areal on IF_IF_arealterminals.ArealDOR=IF_IF_areal.Guid
                            where Cast(zudate as date) BETWEEN @startDate AND @endDate
                            and zuevent = 0 and AreaCustomerField1 = @branch
                            group by cast(zudate as date)
                            order by cast(zudate as date)";

                        accessCommand.Parameters.AddWithValue("@startDate", startDate);
                        accessCommand.Parameters.AddWithValue("@endDate", endDate);
                        accessCommand.Parameters.AddWithValue("@branch", request.Branch);

                        SqlDataReader accessReader = accessCommand.ExecuteReader();

                        while (accessReader.Read())
                        {
                            var record = new DailyAccessRecord
                            {
                                Date = accessReader.GetDateTime(accessReader.GetOrdinal("date")),
                                TotalEmployee = accessReader.GetInt32(accessReader.GetOrdinal("Total Employee"))
                            };
                            records.Add(record);
                        }
                        accessReader.Close();
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


        public static (DateTime startDate, DateTime endDate) GetDateRange(string range)
        {
            DateTime endDate = DateTime.Now.Date;
            DateTime startDate;

            switch (range.ToLower())
            {
                case "past week":
                    startDate = endDate.AddDays(-6);
                    break;
                case "past 3 weeks":
                    startDate = endDate.AddDays(-20);
                    break;
                case "past month":
                    startDate = endDate.AddMonths(-1).AddDays(1);
                    break;
                case "past quarter":
                    startDate = endDate.AddMonths(-3).AddDays(1);
                    break;
                default:
                    throw new ArgumentException("Invalid date range.");
            }

            return (startDate, endDate);
        }



        public class LocationRequest
        {
            public string Location { get; set; }
        }

        public class EmployeeAccessRequest
        {
            public string Branch { get; set; }
            public string DateRange { get; set; }
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
