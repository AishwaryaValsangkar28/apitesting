using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WebApplication4.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReaderController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ReaderController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        //Page 3 - readers and controllers total count
        [HttpGet("controllersAndReadersBySite")]
        public IActionResult GetControllersAndReadersBySite()
        {
            var results = new List<Dictionary<string, object>>();

            try
            {
                using (SqlConnection connection = CreateSqlConnection())
                {
                    connection.Open();
                    Console.WriteLine("Connection successful");

                    // First query to get the readers count
                    var readersCommand = connection.CreateCommand();
                    readersCommand.CommandText = @"
                            SELECT 
                                tdlocation,
                                tdbuilding,
                                COUNT(tdnr) AS Readers
                            FROM IF_IF_tdf
                            WHERE tdbuilding <> ' '
                            GROUP BY tdlocation, tdbuilding
                            ORDER BY tdlocation, tdbuilding";

                    var readers = new Dictionary<string, int>();
                    using (var reader = readersCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = $"{reader["tdlocation"]}-{reader["tdbuilding"]}";
                            readers[key] = (int)reader["Readers"];
                        }
                    }

                    // Second query to get the controllers count
                    var controllersCommand = connection.CreateCommand();
                    controllersCommand.CommandText = @"
                            SELECT 
                                tdlocation,
                                IF_IF_tdf.tdbuilding,
                                COUNT(DISTINCT ponr) AS Controllers
                            FROM IF_IF_portdef
                            JOIN IF_IF_tdf ON IF_IF_portdef.Guid = IF_IF_tdf.PORTDOR
                            WHERE tdbuilding <> ''
                            GROUP BY IF_IF_tdf.tdbuilding, tdlocation";

                    var controllers = new Dictionary<string, int>();
                    using (var reader = controllersCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = $"{reader["tdlocation"]}-{reader["tdbuilding"]}";
                            controllers[key] = (int)reader["Controllers"];
                        }
                    }

                    // Third query to get the offline controllers count
                    var offlineControllersCommand = connection.CreateCommand();
                    offlineControllersCommand.CommandText = @"
                            WITH result AS (
                                SELECT 
                                    MAX(CASE WHEN zuevent=120 THEN zudate ELSE NULL END) AS maxdate,
                                    120 AS zuevent,
                                    tdlocation,
                                    tdbuilding,
                                    COUNT(DISTINCT podesc) AS offlinecount
                                FROM IF_IF_accessarchive
                                JOIN IF_IF_tdf ON IF_IF_tdf.tddesc=IF_IF_accessarchive.zrtddesc
                                JOIN IF_IF_portdef ON IF_IF_portdef.Guid=IF_IF_tdf.PORTDOR
                                WHERE zuevent IN (120, 121)
                                GROUP BY podesc, tdlocation, tdbuilding
                                HAVING MAX(CASE WHEN zuevent=120 THEN zudate ELSE NULL END) > MAX(CASE WHEN zuevent=121 THEN zudate ELSE NULL END)
                            )
                            SELECT 
                                tdlocation, 
                                tdbuilding, 
                                COUNT(offlinecount) AS 'OfflineControllersCount' 
                            FROM result 
                            GROUP BY tdlocation, tdbuilding";

                    var offlineControllers = new Dictionary<string, int>();
                    using (var reader = offlineControllersCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = $"{reader["tdlocation"]}-{reader["tdbuilding"]}";
                            offlineControllers[key] = (int)reader["OfflineControllersCount"];
                        }
                    }

                    // Fourth query to get the offline readers count
                    var offlineReadersCommand = connection.CreateCommand();
                    offlineReadersCommand.CommandText = @"
                            WITH result AS (
                                SELECT 
                                    MAX(CASE WHEN zuevent=17 THEN zudate ELSE NULL END) AS maxdate,
                                    17 AS zuevent,
                                    tdlocation,
                                    tdbuilding,
                                    COUNT(DISTINCT podesc) AS offlinecount
                                FROM IF_IF_accessarchive
                                JOIN IF_IF_tdf ON IF_IF_tdf.tddesc=IF_IF_accessarchive.zrtddesc
                                JOIN IF_IF_portdef ON IF_IF_portdef.Guid=IF_IF_tdf.PORTDOR
                                WHERE zuevent IN (17, 18)
                                GROUP BY tddesc, tdlocation, tdbuilding
                                HAVING MAX(CASE WHEN zuevent=17 THEN zudate ELSE NULL END) > MAX(CASE WHEN zuevent=18 THEN zudate ELSE NULL END)
                            )
                            SELECT 
                                tdlocation, 
                                tdbuilding, 
                                COUNT(offlinecount) AS 'OfflineReadersCount' 
                            FROM result 
                            GROUP BY tdlocation, tdbuilding";

                    var offlineReaders = new Dictionary<string, int>();
                    using (var reader = offlineReadersCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = $"{reader["tdlocation"]}-{reader["tdbuilding"]}";
                            offlineReaders[key] = (int)reader["OfflineReadersCount"];
                        }
                    }

                    // Combine the results
                    var locations = readers.Keys.Union(controllers.Keys).Union(offlineControllers.Keys).Union(offlineReaders.Keys);
                    foreach (var location in locations)
                    {
                        var parts = location.Split('-');
                        var tdlocation = parts[0];
                        var tdbuilding = parts[1];

                        var record = new Dictionary<string, object>
                {
                    { "city", tdlocation },
                    { "location", tdbuilding },
                    { "total controllers", controllers.ContainsKey(location) ? controllers[location] : 0 },
                    { "offline controllers", offlineControllers.ContainsKey(location) ? offlineControllers[location] : 0 },
                    { "total readers", readers.ContainsKey(location) ? readers[location] : 0 },
                    { "offline readers", offlineReaders.ContainsKey(location) ? offlineReaders[location] : 0 }
                };
                        results.Add(record);
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

            if (results.Count > 0)
            {
                return Ok(new { statuscode = 200, data = results });
            }
            else
            {
                return NotFound("No data found.");
            }
        }

        private SqlConnection CreateSqlConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            return new SqlConnection(connectionString);
        }
    }
}
