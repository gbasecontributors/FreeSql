﻿using FreeSql.Internal;
using FreeSql.Internal.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace FreeSql.SqlServer.Curd
{

    class SqlServerSelect<T1> : FreeSql.Internal.CommonProvider.Select1Provider<T1> where T1 : class
    {

        internal static string ToSqlStatic(CommonUtils _commonUtils, CommonExpression _commonExpression, string _select, bool _distinct, string field, StringBuilder _join, StringBuilder _where, string _groupby, string _having, string _orderby, int _skip, int _limit, List<SelectTableInfo> _tables, List<Dictionary<Type, string>> tbUnions, Func<Type, string, string> _aliasRule, List<LambdaExpression> _whereCascadeExpression, IFreeSql _orm)
            => (_commonUtils as SqlServerUtils).IsSelectRowNumber ?
            ToSqlStaticRowNumber(_commonUtils, _commonExpression, _select, _distinct, field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, tbUnions, _aliasRule, _whereCascadeExpression, _orm) :
            ToSqlStaticOffsetFetchNext(_commonUtils, _commonExpression, _select, _distinct, field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, tbUnions, _aliasRule, _whereCascadeExpression, _orm);

        #region SqlServer 2005 row_number
        internal static string ToSqlStaticRowNumber(CommonUtils _commonUtils, CommonExpression _commonExpression, string _select, bool _distinct, string field, StringBuilder _join, StringBuilder _where, string _groupby, string _having, string _orderby, int _skip, int _limit, List<SelectTableInfo> _tables, List<Dictionary<Type, string>> tbUnions, Func<Type, string, string> _aliasRule, List<LambdaExpression> _whereCascadeExpression, IFreeSql _orm)
        {
            if (_orm.CodeFirst.IsAutoSyncStructure)
                _orm.CodeFirst.SyncStructure(_tables.Select(a => a.Table.Type).ToArray());

            if (_whereCascadeExpression.Any())
                foreach (var tb in _tables.Where(a => a.Type != SelectTableInfoType.Parent))
                    tb.Cascade = _commonExpression.GetWhereCascadeSql(tb, _whereCascadeExpression);

            var sb = new StringBuilder();
            var tbUnionsGt0 = tbUnions.Count > 1;
            for (var tbUnionsIdx = 0; tbUnionsIdx < tbUnions.Count; tbUnionsIdx++)
            {
                if (tbUnionsIdx > 0) sb.Append(" \r\n\r\nUNION ALL\r\n\r\n");
                if (tbUnionsGt0) sb.Append("select * from (");
                var tbUnion = tbUnions[tbUnionsIdx];

                var sbnav = new StringBuilder();
                sb.Append(_select);
                if (_distinct) sb.Append("DISTINCT ");
                if (_limit > 0) sb.Append("TOP ").Append(_skip + _limit).Append(" ");
                sb.Append(field);
                if (_skip > 0)
                {
                    if (string.IsNullOrEmpty(_orderby))
                    {
                        var pktb = _tables.Where(a => a.Table.Primarys.Any()).FirstOrDefault();
                        if (pktb != null) _orderby = string.Concat(" \r\nORDER BY ", pktb.Alias, ".", _commonUtils.QuoteSqlName(pktb?.Table.Primarys.First().Attribute.Name));
                        else _orderby = string.Concat(" \r\nORDER BY ", _tables.First().Alias, ".", _commonUtils.QuoteSqlName(_tables.First().Table.Columns.First().Value.Attribute.Name));
                    }
                    sb.Append(", ROW_NUMBER() OVER(").Append(_orderby).Append(") AS __rownum__");
                }
                sb.Append(" \r\nFROM ");
                var tbsjoin = _tables.Where(a => a.Type != SelectTableInfoType.From).ToArray();
                var tbsfrom = _tables.Where(a => a.Type == SelectTableInfoType.From).ToArray();
                for (var a = 0; a < tbsfrom.Length; a++)
                {
                    sb.Append(_commonUtils.QuoteSqlName(tbUnion[tbsfrom[a].Table.Type])).Append(" ").Append(_aliasRule?.Invoke(tbsfrom[a].Table.Type, tbsfrom[a].Alias) ?? tbsfrom[a].Alias);
                    if (tbsjoin.Length > 0)
                    {
                        //如果存在 join 查询，则处理 from t1, t2 改为 from t1 inner join t2 on 1 = 1
                        for (var b = 1; b < tbsfrom.Length; b++)
                        {
                            sb.Append(" \r\nLEFT JOIN ").Append(_commonUtils.QuoteSqlName(tbUnion[tbsfrom[b].Table.Type])).Append(" ").Append(_aliasRule?.Invoke(tbsfrom[b].Table.Type, tbsfrom[b].Alias) ?? tbsfrom[b].Alias);

                            if (string.IsNullOrEmpty(tbsfrom[b].NavigateCondition) && string.IsNullOrEmpty(tbsfrom[b].On) && string.IsNullOrEmpty(tbsfrom[b].Cascade)) sb.Append(" ON 1 = 1");
                            else
                            {
                                var onSql = tbsfrom[b].NavigateCondition ?? tbsfrom[b].On;
                                sb.Append(" ON ").Append(onSql);
                                if (string.IsNullOrEmpty(tbsfrom[b].Cascade) == false)
                                {
                                    if (string.IsNullOrEmpty(onSql)) sb.Append(tbsfrom[b].Cascade);
                                    else sb.Append(" AND (").Append(tbsfrom[b].Cascade).Append(")");
                                }
                            }
                        }
                        break;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(tbsfrom[a].NavigateCondition)) sbnav.Append(" AND (").Append(tbsfrom[a].NavigateCondition).Append(")");
                        if (!string.IsNullOrEmpty(tbsfrom[a].On)) sbnav.Append(" AND (").Append(tbsfrom[a].On).Append(")");
                        if (a > 0 && !string.IsNullOrEmpty(tbsfrom[a].Cascade)) sbnav.Append(" AND (").Append(tbsfrom[a].Cascade).Append(")");
                    }
                    if (a < tbsfrom.Length - 1) sb.Append(", ");
                }
                foreach (var tb in tbsjoin)
                {
                    if (tb.Type == SelectTableInfoType.Parent) continue;
                    switch (tb.Type)
                    {
                        case SelectTableInfoType.LeftJoin:
                            sb.Append(" \r\nLEFT JOIN ");
                            break;
                        case SelectTableInfoType.InnerJoin:
                            sb.Append(" \r\nINNER JOIN ");
                            break;
                        case SelectTableInfoType.RightJoin:
                            sb.Append(" \r\nRIGHT JOIN ");
                            break;
                    }
                    sb.Append(_commonUtils.QuoteSqlName(tbUnion[tb.Table.Type])).Append(" ").Append(_aliasRule?.Invoke(tb.Table.Type, tb.Alias) ?? tb.Alias).Append(" ON ").Append(tb.On ?? tb.NavigateCondition);
                    if (!string.IsNullOrEmpty(tb.Cascade)) sb.Append(" AND (").Append(tb.Cascade).Append(")");
                    if (!string.IsNullOrEmpty(tb.On) && !string.IsNullOrEmpty(tb.NavigateCondition)) sbnav.Append(" AND (").Append(tb.NavigateCondition).Append(")");
                }
                if (_join.Length > 0) sb.Append(_join);

                sbnav.Append(_where);
                if (!string.IsNullOrEmpty(_tables[0].Cascade))
                    sbnav.Append(" AND (").Append(_tables[0].Cascade).Append(")");

                if (sbnav.Length > 0)
                {
                    sb.Append(" \r\nWHERE ").Append(sbnav.Remove(0, 5));
                }
                if (string.IsNullOrEmpty(_groupby) == false)
                {
                    sb.Append(_groupby);
                    if (string.IsNullOrEmpty(_having) == false)
                        sb.Append(" \r\nHAVING ").Append(_having.Substring(5));
                }
                if (_skip <= 0)
                    sb.Append(_orderby);
                else
                    sb.Insert(0, "WITH t AS ( ").Append(" ) SELECT t.* FROM t where __rownum__ > ").Append(_skip);

                sbnav.Clear();
                if (tbUnionsGt0) sb.Append(") ftb");
            }
            return sb.ToString();
        }
        #endregion

        #region SqlServer 2012+ offset feach next
        internal static string ToSqlStaticOffsetFetchNext(CommonUtils _commonUtils, CommonExpression _commonExpression, string _select, bool _distinct, string field, StringBuilder _join, StringBuilder _where, string _groupby, string _having, string _orderby, int _skip, int _limit, List<SelectTableInfo> _tables, List<Dictionary<Type, string>> tbUnions, Func<Type, string, string> _aliasRule, List<LambdaExpression> _whereCascadeExpression, IFreeSql _orm)
        {
            if (_orm.CodeFirst.IsAutoSyncStructure)
                _orm.CodeFirst.SyncStructure(_tables.Select(a => a.Table.Type).ToArray());

            if (_whereCascadeExpression.Any())
                foreach (var tb in _tables.Where(a => a.Type != SelectTableInfoType.Parent))
                    tb.Cascade = _commonExpression.GetWhereCascadeSql(tb, _whereCascadeExpression);

            var sb = new StringBuilder();
            var tbUnionsGt0 = tbUnions.Count > 1;
            for (var tbUnionsIdx = 0; tbUnionsIdx < tbUnions.Count; tbUnionsIdx++)
            {
                if (tbUnionsIdx > 0) sb.Append(" \r\n\r\nUNION ALL\r\n\r\n");
                if (tbUnionsGt0) sb.Append("select * from (");
                var tbUnion = tbUnions[tbUnionsIdx];
                
                var sbnav = new StringBuilder();
                sb.Append(_select);
                if (_distinct) sb.Append("DISTINCT ");
                if (_skip <= 0 && _limit > 0) sb.Append("TOP ").Append(_limit).Append(" ");
                sb.Append(field);
                sb.Append(" \r\nFROM ");
                var tbsjoin = _tables.Where(a => a.Type != SelectTableInfoType.From).ToArray();
                var tbsfrom = _tables.Where(a => a.Type == SelectTableInfoType.From).ToArray();
                for (var a = 0; a < tbsfrom.Length; a++)
                {
                    sb.Append(_commonUtils.QuoteSqlName(tbUnion[tbsfrom[a].Table.Type])).Append(" ").Append(_aliasRule?.Invoke(tbsfrom[a].Table.Type, tbsfrom[a].Alias) ?? tbsfrom[a].Alias);
                    if (tbsjoin.Length > 0)
                    {
                        //如果存在 join 查询，则处理 from t1, t2 改为 from t1 inner join t2 on 1 = 1
                        for (var b = 1; b < tbsfrom.Length; b++)
                        {
                            sb.Append(" \r\nLEFT JOIN ").Append(_commonUtils.QuoteSqlName(tbUnion[tbsfrom[b].Table.Type])).Append(" ").Append(_aliasRule?.Invoke(tbsfrom[b].Table.Type, tbsfrom[b].Alias) ?? tbsfrom[b].Alias);

                            if (string.IsNullOrEmpty(tbsfrom[b].NavigateCondition) && string.IsNullOrEmpty(tbsfrom[b].On) && string.IsNullOrEmpty(tbsfrom[b].Cascade)) sb.Append(" ON 1 = 1");
                            else
                            {
                                var onSql = tbsfrom[b].NavigateCondition ?? tbsfrom[b].On;
                                sb.Append(" ON ").Append(onSql);
                                if (string.IsNullOrEmpty(tbsfrom[b].Cascade) == false)
                                {
                                    if (string.IsNullOrEmpty(onSql)) sb.Append(tbsfrom[b].Cascade);
                                    else sb.Append(" AND (").Append(tbsfrom[b].Cascade).Append(")");
                                }
                            }
                        }
                        break;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(tbsfrom[a].NavigateCondition)) sbnav.Append(" AND (").Append(tbsfrom[a].NavigateCondition).Append(")");
                        if (!string.IsNullOrEmpty(tbsfrom[a].On)) sbnav.Append(" AND (").Append(tbsfrom[a].On).Append(")");
                        if (a > 0 && !string.IsNullOrEmpty(tbsfrom[a].Cascade)) sbnav.Append(" AND (").Append(tbsfrom[a].Cascade).Append(")");
                    }
                    if (a < tbsfrom.Length - 1) sb.Append(", ");
                }
                foreach (var tb in tbsjoin)
                {
                    if (tb.Type == SelectTableInfoType.Parent) continue;
                    switch (tb.Type)
                    {
                        case SelectTableInfoType.LeftJoin:
                            sb.Append(" \r\nLEFT JOIN ");
                            break;
                        case SelectTableInfoType.InnerJoin:
                            sb.Append(" \r\nINNER JOIN ");
                            break;
                        case SelectTableInfoType.RightJoin:
                            sb.Append(" \r\nRIGHT JOIN ");
                            break;
                    }
                    sb.Append(_commonUtils.QuoteSqlName(tbUnion[tb.Table.Type])).Append(" ").Append(_aliasRule?.Invoke(tb.Table.Type, tb.Alias) ?? tb.Alias).Append(" ON ").Append(tb.On ?? tb.NavigateCondition);
                    if (!string.IsNullOrEmpty(tb.Cascade)) sb.Append(" AND (").Append(tb.Cascade).Append(")");
                    if (!string.IsNullOrEmpty(tb.On) && !string.IsNullOrEmpty(tb.NavigateCondition)) sbnav.Append(" AND (").Append(tb.NavigateCondition).Append(")");
                }
                if (_join.Length > 0) sb.Append(_join);

                sbnav.Append(_where);
                if (!string.IsNullOrEmpty(_tables[0].Cascade))
                    sbnav.Append(" AND (").Append(_tables[0].Cascade).Append(")");

                if (sbnav.Length > 0)
                {
                    sb.Append(" \r\nWHERE ").Append(sbnav.Remove(0, 5));
                }
                if (string.IsNullOrEmpty(_groupby) == false)
                {
                    sb.Append(_groupby);
                    if (string.IsNullOrEmpty(_having) == false)
                        sb.Append(" \r\nHAVING ").Append(_having.Substring(5));
                }
                if (_skip > 0)
                {
                    if (string.IsNullOrEmpty(_orderby))
                    {
                        var pktb = _tables.Where(a => a.Table.Primarys.Any()).FirstOrDefault();
                        if (pktb != null) _orderby = string.Concat(" \r\nORDER BY ", pktb.Alias, ".", _commonUtils.QuoteSqlName(pktb?.Table.Primarys.First().Attribute.Name));
                        else _orderby = string.Concat(" \r\nORDER BY ", _tables.First().Alias, ".", _commonUtils.QuoteSqlName(_tables.First().Table.Columns.First().Value.Attribute.Name));
                    }
                    sb.Append(_orderby).Append($" \r\nOFFSET {_skip} ROW");
                    if (_limit > 0) sb.Append($" \r\nFETCH NEXT {_limit} ROW ONLY");
                }
                else
                {
                    sb.Append(_orderby);
                }

                sbnav.Clear();
                if (tbUnionsGt0) sb.Append(") ftb");
            }
            return sb.ToString();
        }
        #endregion

        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) {
            if (FreeSqlSqlServerGlobalExtensions._dicSetGlobalSelectWithLock.TryGetValue(orm, out var tryval))
                this.WithLock(tryval.Item1, tryval.Item2);
        }
        public override ISelect<T1, T2> From<T2>(Expression<Func<ISelectFromExpression<T1>, T2, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3> From<T2, T3>(Expression<Func<ISelectFromExpression<T1>, T2, T3, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3, T4> From<T2, T3, T4>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3, T4>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3, T4, T5> From<T2, T3, T4, T5>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3, T4, T5>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3, T4, T5, T6> From<T2, T3, T4, T5, T6>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3, T4, T5, T6, T7> From<T2, T3, T4, T5, T6, T7>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3, T4, T5, T6, T7, T8> From<T2, T3, T4, T5, T6, T7, T8>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, T8, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3, T4, T5, T6, T7, T8, T9> From<T2, T3, T4, T5, T6, T7, T8, T9>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, T8, T9, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override ISelect<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> From<T2, T3, T4, T5, T6, T7, T8, T9, T10>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, T8, T9, T10, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret, exp?.Parameters); return ret; }
        public override string ToSql(string field = null) => ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2> : FreeSql.Internal.CommonProvider.Select2Provider<T1, T2> where T1 : class where T2 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3> : FreeSql.Internal.CommonProvider.Select3Provider<T1, T2, T3> where T1 : class where T2 : class where T3 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3, T4> : FreeSql.Internal.CommonProvider.Select4Provider<T1, T2, T3, T4> where T1 : class where T2 : class where T3 : class where T4 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3, T4, T5> : FreeSql.Internal.CommonProvider.Select5Provider<T1, T2, T3, T4, T5> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3, T4, T5, T6> : FreeSql.Internal.CommonProvider.Select6Provider<T1, T2, T3, T4, T5, T6> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7> : FreeSql.Internal.CommonProvider.Select7Provider<T1, T2, T3, T4, T5, T6, T7> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8> : FreeSql.Internal.CommonProvider.Select8Provider<T1, T2, T3, T4, T5, T6, T7, T8> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class where T8 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9> : FreeSql.Internal.CommonProvider.Select9Provider<T1, T2, T3, T4, T5, T6, T7, T8, T9> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class where T8 : class where T9 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
    class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : FreeSql.Internal.CommonProvider.Select10Provider<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class where T8 : class where T9 : class where T10 : class
    {
        public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
        public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _commonExpression, _select, _distinct, field ?? this.GetAllFieldExpressionTreeLevel2().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, this.GetTableRuleUnions(), _aliasRule, _whereCascadeExpression, _orm);
    }
}
