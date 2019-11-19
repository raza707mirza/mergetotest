using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mentor.Core.Configuration;
using Mentor.Core.DataEntities;
using Mentor.Core.Helpers;
using Mentor.Core.ViewItems;
using Mentor.ToolBox;
using Microsoft.Linq.Translations;
using Newtonsoft.Json;
using RestSharp;

namespace Mentor.Core.Geography
{
    /// <summary>
    /// The GoogleDistanceMatrix class holds functionality related to using and storing data from DistanceMatrix calls to the Google Api
    /// </summary>
    public static class GoogleDistanceMatrix
    {
        private const string ServiceUri = "https://maps.googleapis.com/maps/api/distancematrix/";

        /// <summary>
        /// Represents the available transportation modes in the Google Api
        /// </summary>
        public enum TransportationMode
        {
            All,
            Unknown,
            Driving,
            Walking,
            Bicycling,
            Transit,
        }

        // These classes represent Json objects returned by the google Api
        #region Google Api Json classes
        internal class DistanceMatrixRequestResult
        {
            public string status { get; set; }
            public IEnumerable<string> origin_addresses { get; set; }
            public IEnumerable<string> destination_addresses { get; set; }
            public IEnumerable<DistanceMatrixRequestResultRow> rows { get; set; }
        }

        internal class DistanceMatrixRequestResultRow
        {
            public IEnumerable<DistanceMatrixRequestResultElement> elements { get; set; }
        }

        internal class DistanceMatrixRequestResultElement
        {
            public string status { get; set; }
            public DistanceMatrixRequestResultValue duration { get; set; }
            public DistanceMatrixRequestResultValue distance { get; set; }
        }

        internal class DistanceMatrixRequestResultValue
        {
            public long value { get; set; }
            public string text { get; set; }
        }

        #endregion

        #region Distance matrix helper classes
        /// <summary>
        /// Represents a single result between two adresses made with the Distance Matrix API
        /// </summary>
        public class DistanceMatrixResult
        {
            public string OriginAddress { get; set; }
            public string DestinationAddress { get; set; }
            public int? OriginAddressId { get; set; }
            public int? DestinationAddressId { get; set; }

            public string ErrorCode { get; set; }
            public TransportationMode Mode { get; set; }
            public decimal DistanceInKilometers { get; set; }
            public TimeSpan Duration { get; set; }
        }

        #endregion

        // Functions usable by other classes to get calculate distances with Google API
        #region Distance matrix calculation functions

        /// <summary>
        /// Get distances between multiple origin adresses and a single destination address from the Google Distance Matrix API
        /// </summary>
        /// <param name="OriginAddresses">Origin adresses to calculate distance FROM</param>
        /// <param name="DestinationAddress">Destination adresses to calculate distances TO</param>
        /// <param name="mode">Transportation mode to calculate distances for</param>
        /// <param name="transitDate">Optional transit date, defaults to next monday at 3 PM</param>
        public static List<DistanceMatrixResult> CalculateDistances(IEnumerable<string> OriginAddresses, string DestinationAddress, TransportationMode mode, DateTime? transitDate = null)
        {
            var responseObject = GetDistanceMatrixResult(OriginAddresses, DestinationAddress, mode, transitDate);

            var results = new List<DistanceMatrixResult>();

            for (var i = 0; i < OriginAddresses.Count(); i++)
            {
                var row = responseObject.rows.ElementAt(i);
                var element = row.elements.FirstOrDefault();

                if (element.status == "OK")
                {
                    results.Add(new DistanceMatrixResult()
                    {
                        Mode = mode,
                        DistanceInKilometers = (decimal)element.distance.value / 1000,
                        Duration = TimeSpan.FromSeconds(element.duration.value),
                        DestinationAddress = DestinationAddress,
                        OriginAddress = OriginAddresses.ElementAt(i)
                    });
                }
                else
                {
                    results.Add(new DistanceMatrixResult()
                    {
                        ErrorCode = element.status,
                        Mode = TransportationMode.Unknown,
                        DestinationAddress = DestinationAddress,
                        OriginAddress = OriginAddresses.ElementAt(i)
                    });
                }

            }
            return results;
        }
        /// <summary>
        /// Get distances between a single origin adresses and a single destination address rom the Google Distance Matrix API
        /// </summary>
        /// <param name="OriginAddress">Origin adress to calculate distance FROM</param>
        /// <param name="DestinationAddress">Destination adresses to calculate distances TO</param>
        /// <param name="mode">Transportation mode to calculate distances for</param>
        /// <param name="transitDate">Optional transit date, defaults to next monday at 3 PM</param>
        public static List<DistanceMatrixResult> CalculateDistances(string OriginAddress, string DestinationAddress, TransportationMode mode, DateTime? transitDate = null)
        {
            var list = new string[1];
            list[0] = OriginAddress;
            var responseObject = GetDistanceMatrixResult(list, DestinationAddress, mode, transitDate);

            var results = new List<DistanceMatrixResult>();


            var row = responseObject.rows.ElementAt(0);
            var element = row.elements.FirstOrDefault();

            if (element.status == "OK")
            {
                results.Add(new DistanceMatrixResult()
                {
                    Mode = mode,
                    DistanceInKilometers = (decimal)element.distance.value / 1000,
                    Duration = TimeSpan.FromSeconds(element.duration.value),
                    DestinationAddress = DestinationAddress,
                    OriginAddress = OriginAddress
                });
            }
            else
            {
                results.Add(new DistanceMatrixResult()
                {
                    ErrorCode = element.status,
                    Mode = TransportationMode.Unknown,
                    DestinationAddress = DestinationAddress,
                    OriginAddress = OriginAddress
                });
            }
            return results;
        }
        /// <summary>
        /// Get distances between multiple origin adresses and a single destination address rom the Google Distance Matrix API
        /// </summary>
        /// <param name="OriginAddresses">Origin adresses to calculate distance FROM</param>
        /// <param name="DestinationAddress">Destination adresses to calculate distances TO</param>
        /// <param name="mode">Transportation mode to calculate distances for</param>
        /// <param name="transitDate">Optional transit date, defaults to next monday at 3 PM</param>
        public static List<DistanceMatrixResult> CalculateDistances(List<MailingAddress> OriginAddresses, string DestinationAddress, TransportationMode mode, DateTime? transitDate = null)
        {
            var originAddressesAsStringList = OriginAddresses.Select(o => o.FullAddress).ToList();

            var responseObject = GetDistanceMatrixResult(originAddressesAsStringList, DestinationAddress, mode, transitDate);

            var results = new List<DistanceMatrixResult>();

            for (var i = 0; i < OriginAddresses.Count(); i++)
            {
                var row = responseObject.rows.ElementAt(i);
                var element = row.elements.FirstOrDefault();

                if (element.status == "OK")
                {
                    results.Add(new DistanceMatrixResult()
                    {
                        Mode = mode,
                        DistanceInKilometers = (decimal)element.distance.value / 1000,
                        Duration = TimeSpan.FromSeconds(element.duration.value),
                        DestinationAddress = DestinationAddress,
                        OriginAddress = OriginAddresses.ElementAt(i).FullAddress,
                        OriginAddressId = OriginAddresses.ElementAt(i).ID,
                    });
                }
                else
                {
                    results.Add(new DistanceMatrixResult()
                    {
                        ErrorCode = element.status,
                        Mode = TransportationMode.Unknown,
                        DestinationAddress = DestinationAddress,
                        OriginAddress = OriginAddresses.ElementAt(i).FullAddress,
                        OriginAddressId = OriginAddresses.ElementAt(i).ID,
                    });
                }

            }
            return results;
        }
        /// <summary>
        /// Get distances between multiple origin adresseses and a single destination address
        /// </summary>
        /// <param name="OriginAddresses">Origin adresses to calculate distance FROM</param>
        /// <param name="DestinationAddress">Destination adresses to calculate distances TO</param>
        /// <param name="mode">Transportation mode to calculate distances for</param>
        /// <param name="transitDate">Optional transit date, defaults to next monday at 3 PM</param>
        public static List<DistanceMatrixResult> CalculateDistances(List<MailingAddress> OriginAddresses, MailingAddress DestinationAddress, TransportationMode mode, DateTime? transitDate = null)
        {
            var originAddressesAsStringList = OriginAddresses.Where(o => o != null).Select(o => o.FullAddress).ToList();

            var responseObject = GetDistanceMatrixResult(originAddressesAsStringList, DestinationAddress.FullAddress, mode, transitDate);

            var results = new List<DistanceMatrixResult>();

            for (var i = 0; i < OriginAddresses.Count(); i++)
            {
                var row = responseObject.rows.ElementAt(i);
                var element = row.elements.FirstOrDefault();

                if (element.status == "OK")
                {
                    results.Add(new DistanceMatrixResult()
                    {
                        Mode = mode,
                        DistanceInKilometers = (decimal)element.distance.value / 1000,
                        Duration = TimeSpan.FromSeconds(element.duration.value),
                        DestinationAddressId = DestinationAddress.ID,
                        OriginAddressId = OriginAddresses.ElementAt(i).ID,
                        DestinationAddress = DestinationAddress.FullAddress,
                        OriginAddress = OriginAddresses.ElementAt(i).FullAddress
                    });
                }
                else
                {
                    results.Add(new DistanceMatrixResult()
                    {
                        ErrorCode = element.status,
                        Mode = TransportationMode.Unknown,
                        DestinationAddressId = DestinationAddress.ID,
                        OriginAddressId = OriginAddresses.ElementAt(i).ID,
                        DestinationAddress = DestinationAddress.FullAddress,
                        OriginAddress = OriginAddresses.ElementAt(i).FullAddress
                    });
                }

            }
            return results;
        }
        /// <summary>
        /// Get distances between multiple origin adresseses and multiple destination addresses
        /// </summary>
        /// <param name="OriginAddresses">Origin adresses to calculate distance FROM</param>
        /// <param name="DestinationAddresses">Destination adresseses to calculate distances TO</param>
        /// <param name="mode">Transportation mode to calculate distances for</param>
        /// <param name="transitDate">Optional transit date, defaults to next monday at 3 PM</param>
        public static List<DistanceMatrixResult> CalculateDistances(List<MailingAddress> OriginAddresses, List<MailingAddress> DestinationAddresses, TransportationMode mode, DateTime? transitDate = null)
        {
            List<List<MailingAddress>> SplitAddresses = new List<List<MailingAddress>>(); // Splitting addresses into lists of 25 each to not exceed the per call limit of google distance matrix.
            var addrSize = 25;

            for (int i = 0; i < DestinationAddresses.Count; i += addrSize)
            {
                SplitAddresses.Add(DestinationAddresses.GetRange(i, Math.Min(addrSize, DestinationAddresses.Count - i)));
            }

            var results = new List<DistanceMatrixResult>();


            if (SplitAddresses.Any()) // if there is ANY destinations, call the google matrix api. Otherwise return a empty list.
            {
                foreach (var destinationAddressesList in SplitAddresses)
                {

                    var responseObject = GetDistanceMatrixResult(OriginAddresses.Select(a => a.FullAddress), destinationAddressesList.Where(o => o != null).Select(o => o.FullAddress).ToList(), mode, transitDate);

                    for (var i = 0; i < OriginAddresses.Count(); i++)
                    {
                        var row = responseObject.rows.ElementAt(i);

                        for (int y = 0; y < destinationAddressesList.Count(); y++)
                        {
                            var element = row.elements.ElementAt(y);

                            if (element.status == "OK")
                            {
                                results.Add(new DistanceMatrixResult()
                                {
                                    Mode = mode,
                                    DistanceInKilometers = (decimal)element.distance.value / 1000,
                                    Duration = TimeSpan.FromSeconds(element.duration.value),
                                    DestinationAddressId = destinationAddressesList.ElementAt(y).ID,
                                    OriginAddressId = OriginAddresses.ElementAt(i).ID,
                                    DestinationAddress = destinationAddressesList.ElementAt(y).FullAddress,
                                    OriginAddress = OriginAddresses.ElementAt(i).FullAddress
                                });
                            }
                            else
                            {
                                results.Add(new DistanceMatrixResult()
                                {
                                    ErrorCode = element.status,
                                    Mode = TransportationMode.Unknown,
                                    DestinationAddressId = destinationAddressesList.ElementAt(y).ID,
                                    OriginAddressId = OriginAddresses.ElementAt(i).ID,
                                    DestinationAddress = destinationAddressesList.ElementAt(y).FullAddress,
                                    OriginAddress = OriginAddresses.ElementAt(i).FullAddress
                                });
                            }
                        }
                    }

                }

            }
            return results;
        }

        #endregion

        // Executing requests with the google Api
        #region Requests

        /// <summary>
        /// Creates a request with the Google Api to get distances between specified adresses.
        /// A result is added for each origin/destination pair.
        /// </summary>
        /// <param name="OriginAddresses">Origin adresses to calculate distances FROM</param>
        /// <param name="DestinationAddress">Destination adress to calculate distances TO</param>
        /// <param name="mode">Transportation mode to calculate distances for</param>
        /// <param name="transitDate">Optional transit date, defaults to next monday at 3 PM</param>
        /// <returns>A DistanceMatrixRequestResult which is a C# class representation of json returned by Google API</returns>
        private static DistanceMatrixRequestResult GetDistanceMatrixResult(IEnumerable<string> OriginAddresses, string DestinationAddress, TransportationMode mode, DateTime? transitDate = null)
        {
            return GetDistanceMatrixResult(OriginAddresses, new List<string>() { DestinationAddress }, mode, transitDate);
        }

        /// <summary>
        /// Creates a request with the Google Api to get distances between specified adresses.
        /// A result is added for each origin/destination pair.
        /// </summary>
        /// <param name="OriginAddresses">Origin adresses to calculate distances FROM</param>
        /// <param name="DestinationAddresses">Destination adresses to calculate distances TO</param>
        /// <param name="mode">Transportation mode to calculate distances for</param>
        /// <param name="transitDate">Optional transit date, defaults to next monday at 3 PM</param>
        /// <returns>A DistanceMatrixRequestResult which is a C# class representation of json returned by Google API</returns>
        private static DistanceMatrixRequestResult GetDistanceMatrixResult(IEnumerable<string> OriginAddresses, IEnumerable<string> DestinationAddresses, TransportationMode mode, DateTime? transitDate = null)
        {
            if (OriginAddresses == null || OriginAddresses.Count() == 0 || DestinationAddresses == null || DestinationAddresses.Count() == 0)
                return new DistanceMatrixRequestResult();

            if (OriginAddresses.Count() * DestinationAddresses.Count() > 100)
                throw new Exception("Google API does not allow more than 100 distances to be calculated per request");

            RestClient client = new RestClient(ServiceUri);

            var request = new RestRequest("json", Method.GET);

            request.AddParameter("key", AppSettings.GoogleDistanceMatrixApiKey, ParameterType.GetOrPost);
            request.AddParameter("client", "gme-mentordanmark");
            request.AddParameter("channel", AppSettings.GoogleChannelName);
            request.AddParameter("destinations", string.Join("|", DestinationAddresses), ParameterType.GetOrPost);

            if (mode == TransportationMode.Walking)
                request.AddParameter("mode", "walking", ParameterType.GetOrPost);
            else if (mode == TransportationMode.Bicycling)
                request.AddParameter("mode", "bicycling", ParameterType.GetOrPost);
            else if (mode == TransportationMode.Driving)
                request.AddParameter("mode", "driving", ParameterType.GetOrPost);
            else if (mode == TransportationMode.Transit)
            {
                request.AddParameter("mode", "transit", ParameterType.GetOrPost);

                if (transitDate == null)
                {
                    // If no date is specified, use next monday at 15
                    transitDate = Calculator.NextSpecifiedWeekday(DateTime.Today, DayOfWeek.Monday).AddHours(15);
                }

                /*
                 The desired time of departure. You can specify the time as an integer in seconds since midnight, 
                 * January 1, 1970 UTC. Alternatively, you can specify a value of now, which sets the departure time to 
                 * the current time (correct to the nearest second).                  
                 */

                var utcSeconds = transitDate.Value.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                request.AddParameter("departure_time", utcSeconds, ParameterType.GetOrPost);

            }
            else if (mode == TransportationMode.Unknown)
                throw new Exception("Transportation mode unknown");

            request.AddParameter("origins", string.Join("|", OriginAddresses), ParameterType.GetOrPost);

            var response = WebHelper.GetWebRequestResponse(client, request);

            var responseObject = (DistanceMatrixRequestResult)JsonConvert.DeserializeObject(response.Content, typeof(DistanceMatrixRequestResult));

            return responseObject;
        }

        #endregion
    }






}
