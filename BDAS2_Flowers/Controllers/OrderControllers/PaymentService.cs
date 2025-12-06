using System;
using System.Linq;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.OrderControllers
{
    /// <summary>
    /// Rozhraní služby pro práci s platbami.
    /// Zajišťuje vytvoření platby a uložení detailů podle typu platby
    /// (karta, hotovost) a práci s vrácenou hotovostí.
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Vytvoří novou platbu daného typu pro uživatele
        /// a vrátí její interní identifikátor.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="userId">ID uživatele, pro kterého je platba vytvářena.</param>
        /// <param name="type">Typ platby (např. "card", "cash", "cupon").</param>
        /// <returns>Identifikátor vytvořené platby.</returns>
        Task<int> CreatePaymentAsync(OracleConnection con, OracleTransaction tx, int userId, string type);

        /// <summary>
        /// Uloží k platbě informaci o kartě (poslední 4 číslice).
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby, ke které se karta váže.</param>
        /// <param name="cardNumber">Číslo karty zadané uživatelem.</param>
        Task AttachCardAsync(OracleConnection con, OracleTransaction tx, int paymentId, string cardNumber);

        /// <summary>
        /// Uloží k platbě informaci o přijaté hotovosti.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby, ke které se hotovost váže.</param>
        /// <param name="accepted">Celková částka převzaté hotovosti.</param>
        Task AttachCashAsync(OracleConnection con, OracleTransaction tx, int paymentId, decimal accepted);

        /// <summary>
        /// Vrátí částku platby na základě jejího ID z pohledu <c>VW_ADMIN_PAYMENTS</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby.</param>
        /// <returns>Částka platby, nebo 0, pokud záznam neexistuje.</returns>
        Task<decimal> GetAmountAsync(OracleConnection con, OracleTransaction tx, int paymentId);

        /// <summary>
        /// Nastaví u platby výši vrácené hotovosti.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby.</param>
        /// <param name="change">Výše vrácené hotovosti zákazníkovi.</param>
        Task SetCashChangeAsync(OracleConnection con, OracleTransaction tx, int paymentId, decimal change);
    }

    /// <summary>
    /// Implementace služby pro práci s platbami nad Oracle databází.
    /// Volá odpovídající uložené procedury pro vytvoření platby,
    /// přiřazení platebních detailů a zjištění částky.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        /// <summary>
        /// Vytvoří novou platbu daného typu pro uživatele
        /// pomocí procedury <c>PRC_CREATE_PAYMENT</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="userId">ID uživatele, pro kterého je platba vytvářena.</param>
        /// <param name="type">Typ platby (např. "card", "cash", "cupon").</param>
        /// <returns>Identifikátor nově vytvořené platby.</returns>
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

        /// <summary>
        /// K existující platbě uloží poslední 4 číslice čísla karty
        /// pomocí procedury <c>PRC_PAYMENT_ATTACH_CARD</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby.</param>
        /// <param name="cardNumber">Číslo platební karty zadané uživatelem.</param>
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

        /// <summary>
        /// K existující platbě uloží informaci o přijaté hotovosti
        /// pomocí procedury <c>PRC_PAYMENT_ATTACH_CASH</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby.</param>
        /// <param name="accepted">Částka převzaté hotovosti.</param>
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

        /// <summary>
        /// Nastaví hodnotu vrácené hotovosti k platbě
        /// pomocí procedury <c>PRC_PAYMENT_SET_CHANGE</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby.</param>
        /// <param name="change">Výše vrácené hotovosti zákazníkovi.</param>
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

        /// <summary>
        /// Zjistí částku platby z pohledu <c>VW_ADMIN_PAYMENTS</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby.</param>
        /// <returns>Výše částky platby nebo 0, pokud záznam neexistuje.</returns>
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

        /// <summary>
        /// Vrátí poslední čtyři číslice zadaného čísla,
        /// použito pro uložení posledních 4 číslic karty.
        /// </summary>
        /// <param name="input">Původní vstupní řetězec (číslo karty, může obsahovat mezery apod.).</param>
        /// <returns>Číslo z posledních 4 číslic, nebo 0, pokud nelze převést.</returns>
        private static int GetLast4Digits(string input)
        {
            var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return 0;
            var last4 = digits.Length >= 4 ? digits[^4..] : digits;
            return int.TryParse(last4, out var n) ? n : 0;
        }
    }
}
