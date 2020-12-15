﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Threading.Tasks;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace QueryGraphicBoundary.Ribbon
{
	/// <summary>
	/// Generate an outline geometry for the selected feature
	/// </summary>
	/// <remarks>Supports points, lines, polygons, and annotation features</remarks>
	internal class UpdateOutline : MapTool
	{

		private double _bufferDist = 0;
		private CIMSymbolReference _polyOutline = null;

		public UpdateOutline()
		{
			IsSketchTool = true;
			SketchType = SketchGeometryType.Point;
			SketchOutputMode = SketchOutputMode.Map;
		}

		protected override Task OnToolActivateAsync(bool active)
		{
			var center = MapView.Active.Extent.Center;
			return QueuedTask.Run(() =>
			{
				if (_polyOutline == null)
				{
					_polyOutline = SymbolFactory.Instance.ConstructPolygonSymbol(
													 null, SymbolFactory.Instance.ConstructStroke(
														 ColorFactory.Instance.BlueRGB, 1.5)).MakeSymbolReference();
				}
				var clientPt = MapView.Active.MapToClient(MapView.Active.Extent.Center);
				clientPt.X += SelectionEnvironment.SelectionTolerance;
				var mapPt = MapView.Active.ClientToMap(clientPt);
				_bufferDist = Math.Abs(center.X - mapPt.X);
			});
		}

		protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
		{
			var mv = MapView.Active;
			Geometry sel_geom = null;
			IDisposable graphic = null;
			//Flash the selection geometry so we can see where we clicked
			QueuedTask.Run(() =>
			{
				sel_geom = GeometryEngine.Instance.Buffer(geometry, _bufferDist) as Polygon;
				graphic = mv.AddOverlay(sel_geom, _polyOutline);
			});

			return QueuedTask.Run(() =>
			{

				var sel = mv.SelectFeatures(sel_geom);
				var sel_first = sel.FirstOrDefault(kvp => kvp.Value.Count() > 0);
				if (sel_first.Key == null)
				{
					graphic?.Dispose();
					return true; //nothing selected
				}

				var outlineType = Module1.Current.SelectedMaskKind == MaskKind.Box ?
												 DrawingOutlineType.BoundingEnvelope :
												 DrawingOutlineType.Exact;

				var feat_layer = sel_first.Key;
				bool isAnno = feat_layer is AnnotationLayer;
				var oid = sel_first.Value.First();
				var outline = feat_layer.QueryDrawingOutline(oid, MapView.Active, outlineType);

				graphic?.Dispose();

				//post process
				var mask = Module1.Current.PostProcess(
					outline, feat_layer.ShapeType, Module1.Current.Margin, Module1.Current.SelectedMaskKind, isAnno)
										 as Polygon;
				Module1.Current.AddOutlines(new List<Polygon> { mask }, Module1.Current.SelectedMaskKind);
				feat_layer.ClearSelection();
				return true;
			});
		}
	}
}
