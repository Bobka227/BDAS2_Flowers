using System;
using System.Linq;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.OrderControllers
{
    public interface IPaymentService
    {
        Task<int> CreatePaymentAsync(OracleConnection con, OracleTransaction tx, int userId, string type);
        Task AttachCardAsync(OracleConnection con, OracleTransaction tx, int paymentId, string cardNumber);
        Task AttachCashAsync(OracleConnection con, OracleTransaction tx, int paymentId, decimal accepted);
        Task<decimal> GetAmountAsync(OracleConnection con, OracleTransaction tx, int paymentId);
        Task SetCashChangeAsync(OracleConnection con, OracleTransaction tx, int paymentId, decimal change);
    }

    public class PaymentService : IPaymentService
    {
        public async Task<int> CreatePaymentAsync(OracleConnection con, OracleTransaction tx, int userId, string type)
        {
            await using var cmd = new OracleCommand("PRC_CREATE_PAYMENT", con)
            {
                CommandType = CommandType.StoredProcedure,
                Transaction = tx
            };
            cmd.BindByName = true;
            cmd.Parameters.Add("p_user_id", OracleDbType.Int32).Value = userId;
            cmd.Parameters.Add("p_type", OracleDbType.Varchar2, 10).Value = type;
            var o = new OracleParameter("o_payment_id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(o);

            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(o.Value.ToString());
        }

        public async Task AttachCardAsync(OracleConnection con, OracleTransaction tx, int paymentId, string cardNumber)
        {
            var last4 = GetLast4Digits(cardNumber);

            await using var cmd = new OracleCommand("PRC_PAYMENT_ATTACH_CARD", con)
            {
                CommandType = CommandType.StoredProcedure,
                Transaction = tx
            };
            cmd.BindByName = true;
            cmd.Parameters.Add("p_payment_id", OracleDbType.Int32).Value = paymentId;
            cmd.Parameters.Add("p_card_last4", OracleDbType.Int32).Value = last4;

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task AttachCashAsync(OracleConnection con, OracleTransaction tx, int paymentId, decimal accepted)
        {
            await using var cmd = new OracleCommand("PRC_PAYMENT_ATTACH_CASH", con)
            {
                CommandType = CommandType.StoredProcedure,
                Transaction = tx
            };
            cmd.BindByName = true;
            cmd.Parameters.Add("p_payment_id", OracleDbType.Int32).Value = paymentId;
            cmd.Parameters.Add("p_accepted", OracleDbType.Decimal).Value = accepted;

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SetCashChangeAsync(OracleConnection con, OracleTransaction tx, int paymentId, decimal change)
        {
            await using var cmd = new OracleCommand("PRC_PAYMENT_SET_CHANGE", con)
            {
                CommandType = CommandType.StoredProcedure,
                Transaction = tx
            };
            cmd.BindByName = true;
            cmd.Parameters.Add("p_payment_id", OracleDbType.Int32).Value = paymentId;
            cmd.Parameters.Add("p_change", OracleDbType.Decimal).Value = change;

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<decimal> GetAmountAsync(OracleConnection con, OracleTransaction tx, int paymentId)
        {
            await using var cmd = new OracleCommand(
                @"SELECT AMOUNT 
                FROM VW_ADMIN_PAYMENTS 
                WHERE ID = :pid", con) 
            {
                CommandType = CommandType.Text,
                Transaction = tx
            };

            cmd.BindByName = true;
            cmd.Parameters.Add("pid", OracleDbType.Int32).Value = paymentId;

            var v = await cmd.ExecuteScalarAsync();
            return v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);
        }


        private static int GetLast4Digits(string input)
        {
            var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return 0;
            var last4 = digits.Length >= 4 ? digits[^4..] : digits;
            return int.TryParse(last4, out var n) ? n : 0;
        }
    }
}
