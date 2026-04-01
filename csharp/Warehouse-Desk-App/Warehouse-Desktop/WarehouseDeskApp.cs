using System;
using System.Collections.Generic;
using System.Linq;

namespace Warehouse_Desktop
{
    public class WarehouseDeskApp
    {
        private readonly Dictionary<string, int> stockBySku = new Dictionary<string, int>();
        private readonly Dictionary<string, int> reservedBySku = new Dictionary<string, int>();
        private readonly Dictionary<string, double> priceBySku = new Dictionary<string, double>();
        private readonly Dictionary<string, string> orderStatus = new Dictionary<string, string>();
        private readonly Dictionary<string, string> orderSku = new Dictionary<string, string>();
        private readonly Dictionary<string, int> orderQty = new Dictionary<string, int>();
        private readonly Dictionary<string, string> reservationSku = new Dictionary<string, string>();
        private readonly Dictionary<string, int> reservationQty = new Dictionary<string, int>();
        private readonly Dictionary<string, DateTime> reservationExpiry = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, string> reservationCustomer = new Dictionary<string, string>();
        private readonly List<string> eventLog = new List<string>();
        public IReadOnlyList<string> EventLog => eventLog.AsReadOnly();
        private double cashBalance;
        private int nextOrderNumber;

        public void SeedData()
        {
            stockBySku["PEN-BLACK"] = 40;
            stockBySku["PEN-BLUE"] = 25;
            stockBySku["NOTE-A5"] = 15;
            stockBySku["STAPLER"] = 4;

            reservedBySku["PEN-BLACK"] = 0;
            reservedBySku["PEN-BLUE"] = 0;
            reservedBySku["NOTE-A5"] = 0;
            reservedBySku["STAPLER"] = 0;

            priceBySku["PEN-BLACK"] = 1.5;
            priceBySku["PEN-BLUE"] = 1.6;
            priceBySku["NOTE-A5"] = 4.0;
            priceBySku["STAPLER"] = 12.0;

            cashBalance = 300.0;
            nextOrderNumber = 1001;
        }

        public void RunDemoDay()
        {
            var commands = new List<string>
            {
                "RECV;NOTE-A5;5;2.20",
                "SELL;alice;PEN-BLACK;10",
                "SELL;bob;STAPLER;5",
                "CANCEL;O1002",
                "COUNT;STAPLER",
                "SELL;carol;STAPLER;2",
                "SELL;dan;NOTE-A5;14",
                "COUNT;NOTE-A5",
                "DUMP"
            };

            foreach (var command in commands)
            {
                ProcessLine(command);
            }
            PrintEndOfDayReport();
        }

        public void ProcessLine(string line)
        {
            ProcessLine(line, DateTime.Now);
        }

        public void ProcessLine(string line, DateTime currentTime)
        {
            ExpireReservations(currentTime);
            string[] parts = line.Split(';');
            string type = parts[0];

            if ("RECV".Equals(type))
            {
                string sku = parts[1];
                int qty = ParseInt(parts[2]);
                double unitCost = ParseDouble(parts[3]);
                int current = stockBySku.GetValueOrDefault(sku, 0);
                stockBySku[sku] = current + qty;
                cashBalance = cashBalance - (qty * unitCost);
                eventLog.Add("received " + qty + " of " + sku + " at " + unitCost);
                return;
            }

            if ("SELL".Equals(type))
            {
                string customer = parts[1];
                string sku = parts[2];
                int qty = ParseInt(parts[3]);
                string orderId = "O" + nextOrderNumber;
                nextOrderNumber = nextOrderNumber + 1;
                orderSku[orderId] = sku;
                orderQty[orderId] = qty;

                int onHand = stockBySku.GetValueOrDefault(sku, 0);
                int reserved = reservedBySku.GetValueOrDefault(sku, 0);
                int available = onHand - reserved;
                if (available < qty)
                {
                    orderStatus[orderId] = "BACKORDER";
                    eventLog.Add("order " + orderId + " backordered for " + customer + " sku=" + sku + " qty=" + qty);
                }
                else
                {
                    stockBySku[sku] = onHand - qty;
                    double unitPrice = priceBySku.GetValueOrDefault(sku, 0.0);
                    double orderTotal = unitPrice * qty;
                    cashBalance = cashBalance + orderTotal;
                    orderStatus[orderId] = "SHIPPED";
                    eventLog.Add("order " + orderId + " shipped to " + customer + " amount=" + orderTotal);
                }
                return;
            }

            if ("CANCEL".Equals(type))
            {
                string orderId = parts[1];
                if (!orderStatus.TryGetValue(orderId, out var status))
                {
                    eventLog.Add("cannot cancel " + orderId + " because it does not exist");
                    return;
                }

                if ("BACKORDER".Equals(status))
                {
                    orderStatus[orderId] = "CANCELLED";
                    eventLog.Add("cancelled backorder " + orderId);
                    return;
                }

                if ("SHIPPED".Equals(status))
                {
                    string sku = orderSku[orderId];
                    int qty = orderQty.GetValueOrDefault(orderId, 0);
                    int current = stockBySku.GetValueOrDefault(sku, 0);
                    stockBySku[sku] = current + qty;
                    double unitPrice = priceBySku.GetValueOrDefault(sku, 0.0);
                    cashBalance = cashBalance - (unitPrice * qty);
                    orderStatus[orderId] = "CANCELLED_AFTER_SHIP";
                    eventLog.Add("cancelled shipped order " + orderId + " with restock");
                    return;
                }

                eventLog.Add("order " + orderId + " could not be cancelled from state " + status);
                return;
            }

            if ("COUNT".Equals(type))
            {
                string sku = parts[1];
                int onHand = stockBySku.GetValueOrDefault(sku, 0);
                int reserved = reservedBySku.GetValueOrDefault(sku, 0);
                int available = onHand - reserved;
                eventLog.Add("count " + sku + " onHand=" + onHand + " reserved=" + reserved + " available=" + available);
                return;
            }

            if ("RESERVE".Equals(type))
            {
                string customer = parts[1];
                string sku = parts[2];
                int qty = ParseInt(parts[3]);
                int minutes = ParseInt(parts[4]);

                int onHand = stockBySku.GetValueOrDefault(sku, 0);
                int reserved = reservedBySku.GetValueOrDefault(sku, 0);
                int available = onHand - reserved;

                if (available < qty)
                {
                    eventLog.Add("cannot reserve " + qty + " of " + sku + " for " + customer + ": insufficient stock");
                }
                else
                {
                    string resId = "R" + nextOrderNumber;
                    nextOrderNumber++;
                    reservationSku[resId] = sku;
                    reservationQty[resId] = qty;
                    reservationCustomer[resId] = customer;
                    reservationExpiry[resId] = currentTime.AddMinutes(minutes);

                    reservedBySku[sku] = reserved + qty;
                    eventLog.Add("reserved " + qty + " of " + sku + " for " + customer + " (id=" + resId + ")");
                }
                return;
            }

            if ("CONFIRM".Equals(type))
            {
                string resId = parts[1];
                if (reservationSku.TryGetValue(resId, out string? sku))
                {
                    int qty = reservationQty[resId];
                    string customer = reservationCustomer[resId];

                    // Ship it
                    stockBySku[sku] -= qty;
                    reservedBySku[sku] -= qty;

                    string orderId = "O" + nextOrderNumber++;
                    orderSku[orderId] = sku;
                    orderQty[orderId] = qty;
                    orderStatus[orderId] = "SHIPPED";

                    double unitPrice = priceBySku.GetValueOrDefault(sku, 0.0);
                    double orderTotal = unitPrice * qty;
                    cashBalance += orderTotal;

                    // Remove reservation
                    RemoveReservation(resId);

                    eventLog.Add("reservation " + resId + " confirmed and shipped as " + orderId);
                }
                else
                {
                    eventLog.Add("cannot confirm " + resId + ": reservation expired or not found");
                }
                return;
            }

            if ("RELEASE".Equals(type))
            {
                string resId = parts[1];
                if (reservationSku.TryGetValue(resId, out string? sku))
                {
                    int qty = reservationQty[resId];
                    reservedBySku[sku] -= qty;
                    RemoveReservation(resId);
                    eventLog.Add("reservation " + resId + " released");
                }
                else
                {
                    eventLog.Add("cannot release " + resId + ": reservation expired or not found");
                }
                return;
            }

            if ("DUMP".Equals(type))
            {
                Console.WriteLine("---- dump ----");
                Console.WriteLine("stock={" + string.Join(", ", stockBySku.Select(kvp => kvp.Key + "=" + kvp.Value)) + "}");
                Console.WriteLine("reserved={" + string.Join(", ", reservedBySku.Select(kvp => kvp.Key + "=" + kvp.Value)) + "}");
                Console.WriteLine("orders={" + string.Join(", ", orderStatus.Select(kvp => kvp.Key + "=" + kvp.Value)) + "}");
                Console.WriteLine("cashBalance=" + cashBalance);
                return;
            }

            eventLog.Add("unknown command: " + line);
        }

        private void ExpireReservations(DateTime currentTime)
        {
            var expiredIds = reservationExpiry
                .Where(kvp => kvp.Value <= currentTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var resId in expiredIds)
            {
                string sku = reservationSku[resId];
                int qty = reservationQty[resId];
                reservedBySku[sku] -= qty;
                RemoveReservation(resId);
                eventLog.Add("reservation " + resId + " expired");
            }
        }

        private void RemoveReservation(string resId)
        {
            reservationSku.Remove(resId);
            reservationQty.Remove(resId);
            reservationCustomer.Remove(resId);
            reservationExpiry.Remove(resId);
        }

        private int ParseInt(string value)
        {
            return int.Parse(value.Trim());
        }

        private double ParseDouble(string value)
        {
            return double.Parse(value.Trim());
        }

        public void PrintEndOfDayReport()
        {
            int shipped = 0;
            int backorder = 0;
            int cancelled = 0;
            foreach (string status in orderStatus.Values)
            {
                if ("SHIPPED".Equals(status))
                {
                    shipped = shipped + 1;
                }
                else if ("BACKORDER".Equals(status))
                {
                    backorder = backorder + 1;
                }
                else if (status.StartsWith("CANCELLED"))
                {
                    cancelled = cancelled + 1;
                }
            }

            List<string> lowStock = new List<string>();
            foreach (var item in stockBySku)
            {
                if (item.Value < 5)
                {
                    lowStock.Add(item.Key);
                }
            }

            Console.WriteLine();
            Console.WriteLine("==== end of day ====");
            Console.WriteLine("orders shipped: " + shipped);
            Console.WriteLine("orders backordered: " + backorder);
            Console.WriteLine("orders cancelled: " + cancelled);
            Console.WriteLine("cash balance: " + string.Format("{0:F2}", cashBalance));
            Console.WriteLine("low stock skus: [" + string.Join(", ", lowStock) + "]");
            Console.WriteLine();
            Console.WriteLine("events:");
            foreach (string @event in eventLog)
            {
                Console.WriteLine(" - " + @event);
            }
        }
    }
}
