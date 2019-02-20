﻿#region License
/*
Copyright (c) 2018 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Ser.Connections
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Qlik.EngineAPI;
    #endregion

    public class QlikDimensions
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        private List<DimensionDataHelper> Dimensions { get; set; }
        private IDoc SenseApp { get; set; }
        #endregion

        #region Constructor
        public QlikDimensions(IDoc senseApp)
        {
            SenseApp = senseApp;
            Dimensions = GetDimensionListAsync().Result;
        }
        #endregion

        #region Private Methods
        private async Task<List<DimensionDataHelper>> GetDimensionListAsync()
        {
            try
            {
                var request = JObject.FromObject(new
                {
                    qProp = new
                    {
                        qInfo = new
                        {
                            qType = "DimensionList"
                        },
                        qDimensionListDef = new
                        {
                            qType = "dimension",
                            qData = new
                            {
                                grouping = "/qDim"
                            }
                        }
                    }
                });

                return await SenseApp.CreateSessionObjectAsync(request)
                .ContinueWith((res) =>
                {
                    return res.Result.GetLayoutAsync<JObject>();
                })
                .Unwrap()
                .ContinueWith<List<DimensionDataHelper>>((res2) =>
                {
                    var ret = res2.Result as dynamic;
                    var dimList = ret.qDimensionList;
                    var result = new List<DimensionDataHelper>();
                    foreach (var qitem in dimList.qItems)
                    {
                        var defs = qitem.qData.grouping.qFieldDefs as JToken;
                        var grouping = qitem.qData.grouping.qGrouping.ToObject<NxGrpType>();
                        result.Add(new DimensionDataHelper()
                        {
                            Id = qitem.qInfo.qId,
                            Title = qitem.qMeta.title,
                            Grouping = grouping,
                            FieldDefs = defs.ToObject<List<string>>(),
                        });
                    }
                    return result;
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Can´t initialize dimensions.");
                return null;
            }
        }

        private List<QlikListbox> GetFieldDefs(string filterText)
        {
            try
            {
                var results = new List<QlikListbox>();
                foreach (var dim in Dimensions)
                {
                    if (dim.Grouping == NxGrpType.GRP_NX_HIEARCHY ||
                        dim.Grouping == NxGrpType.GRP_NX_NONE)
                    {
                        if (dim.Title == filterText)
                        {
                            foreach (var fieldDef in dim.FieldDefs)
                                results.Add(new QlikListbox(fieldDef, SenseApp));
                        }
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetFieldDefs)}\" has an error.");
                return null;
            }
        }
        #endregion

        #region Public Methods
        public QlikListbox GetSelections(string filterText)
        {
            try
            {
                var listbox = GetFieldDefs(filterText).FirstOrDefault() ?? null;
                if (listbox != null)
                {
                    logger.Info($"The master element \"{filterText}\" was found.");
                    return listbox;
                }
                else
                {
                    logger.Info($"The filter text \"{filterText}\" is not a master element.");
                    return new QlikListbox(filterText, SenseApp);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("The selection could not be created.", ex);
            }
        }

        public List<QlikListbox> GetListboxList(List<string> filterTexts)
        {
            try
            {
                var results = new List<QlikListbox>();
                foreach (var text in filterTexts)
                {
                    var listboxes = GetFieldDefs(text);
                    if (listboxes.Count > 0)
                    {
                        results.AddRange(listboxes);
                        logger.Info($"The master element \"{text}\" was found.");
                    }
                    else
                    {
                        results.Add(new QlikListbox(text, SenseApp));
                        logger.Info($"The filter text \"{text}\" is not a master element.");
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                throw new Exception("The selection could not be created.", ex);
            }
        }
        #endregion
    }

    public class DimensionDataHelper
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<string> FieldDefs { get; set; }
        public NxGrpType Grouping { get; set; }
    }
}