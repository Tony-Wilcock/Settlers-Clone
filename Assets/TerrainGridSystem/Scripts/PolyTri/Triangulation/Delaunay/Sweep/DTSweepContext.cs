﻿/* Poly2Tri
 * Copyright (c) 2009-2010, Poly2Tri Contributors
 * http://code.google.com/p/poly2tri/
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 * * Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 * * Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 * * Neither the name of Poly2Tri nor the names of its contributors may be
 *   used to endorse or promote products derived from this software without specific
 *   prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;


namespace TGS.Poly2Tri {
	/**
     * 
     * @author Thomas Åhlén, thahlen@gmail.com
     *
     */
	public class DTSweepContext : TriangulationContext {
		// Inital triangle factor, seed triangle will extend 30% of 
		// PointSet width to both left and right.
		private readonly float ALPHA = 0.3f;
		public AdvancingFront Front;

		public TriangulationPoint Head { get; set; }

		public TriangulationPoint Tail { get; set; }

		public DTSweepBasin Basin = new DTSweepBasin ();
		public DTSweepEdgeEvent EdgeEvent = new DTSweepEdgeEvent ();
		private DTSweepPointComparator _comparator = new DTSweepPointComparator ();

		public override TriangulationAlgorithm Algorithm { get { return TriangulationAlgorithm.DTSweep; } }

		public DTSweepContext () {
			Clear ();
		}


		public void RemoveFromList (DelaunayTriangle triangle) {
			Triangles.Remove (triangle);
			// TODO: remove all neighbor pointers to this triangle
			//        for( int i=0; i<3; i++ )
			//        {
			//            if( triangle.neighbors[i] != null )
			//            {
			//                triangle.neighbors[i].clearNeighbor( triangle );
			//            }
			//        }
			//        triangle.clearNeighbors();
		}

		public void MeshClean (DelaunayTriangle triangle) {
			MeshCleanReq (triangle);
		}

		private void MeshCleanReq (DelaunayTriangle triangle) {
			//			if (triangle != null && !triangle.IsInterior) {
			//				triangle.IsInterior = true;
			//				Triangulatable.AddTriangle (triangle);
			//
			//				for (int i = 0; i < 3; i++) {
			//					if (!triangle.EdgeIsConstrained [i]) {
			//						MeshCleanReq (triangle.Neighbors [i]);
			//					}
			//				}
			//			}
			
			List<DelaunayTriangle> tris = new List<DelaunayTriangle>(Triangles.Count);
			tris.Add(triangle);
			
			while(tris.Count>0) {
				DelaunayTriangle t = tris[tris.Count-1];
				tris.RemoveAt (tris.Count-1);
				
				if (t != null && !t.IsInterior) {
					t.IsInterior = true;
					Triangulatable.AddTriangle (t);
					for (int i = 0; i < 3; i++) {
						if (!t.EdgeIsConstrained[i]) {
							tris.Add (t.Neighbors[i]);
						}
					}
				}
			}
			
		}

		public override void Clear () {
			base.Clear ();
			Triangles.Clear ();
		}

//		public void AddNode (AdvancingFrontNode node) {
//			//        Console.WriteLine( "add:" + node.key + ":" + System.identityHashCode(node.key));
//			//        m_nodeTree.put( node.getKey(), node );
//			Front.AddNode (node);
//		}
//
//		public void RemoveNode (AdvancingFrontNode node) {
//			//        Console.WriteLine( "remove:" + node.key + ":" + System.identityHashCode(node.key));
//			//        m_nodeTree.delete( node.getKey() );
//			Front.RemoveNode (node);
//		}

		public AdvancingFrontNode LocateNode (TriangulationPoint point) {
			return Front.LocateNode (point);
		}

		public void CreateAdvancingFront () {
			AdvancingFrontNode head, tail, middle;
			// Initial triangle
			DelaunayTriangle iTriangle = new DelaunayTriangle (Points [0], Tail, Head);
			Triangles.Add (iTriangle);

			head = new AdvancingFrontNode (iTriangle.Points [1]);
			head.Triangle = iTriangle;
			middle = new AdvancingFrontNode (iTriangle.Points [0]);
			middle.Triangle = iTriangle;
			tail = new AdvancingFrontNode (iTriangle.Points [2]);

			Front = new AdvancingFront (head, tail);
//			Front.AddNode (middle);

			// TODO: I think it would be more intuitive if head is middles next and not previous
			//       so swap head and tail
			Front.Head.Next = middle;
			middle.Next = Front.Tail;
			middle.Prev = Front.Head;
			Front.Tail.Prev = middle;
		}


		/// <summary>
		/// Try to map a node to all sides of this triangle that don't have 
		/// a neighbor.
		/// </summary>
		public void MapTriangleToNodes (DelaunayTriangle t) {
			for (int i = 0; i < 3; i++) {
				if (t.Neighbors [i] == null) {
					AdvancingFrontNode n = Front.LocatePoint (t.PointCWFrom (t.Points [i]));
					if (n != null) {
						n.Triangle = t;
					}
				}
			}
		}

		public override void PrepareTriangulation (ITriangulatable t) {
			base.PrepareTriangulation (t);

			double xmax, xmin;
			double ymax, ymin;

			xmax = xmin = Points [0].mX;
			ymax = ymin = Points [0].mY;

			// Calculate bounds. Should be combined with the sorting
			int pointCount = Points.Count;
			for (int k = 0; k < pointCount; k++) {
				TriangulationPoint p = Points[k];
				if (p.mX > xmax) {
					xmax = p.mX;
				}
				if (p.X < xmin) {
					xmin = p.mX;
				}
				if (p.Y > ymax) {
					ymax = p.mY;
				}
				if (p.Y < ymin) {
					ymin = p.mY;
				}
			}

			double deltaX = ALPHA * (xmax - xmin);
			double deltaY = ALPHA * (ymax - ymin);
			TriangulationPoint p1 = new TriangulationPoint (xmax + deltaX, ymin - deltaY);
			TriangulationPoint p2 = new TriangulationPoint (xmin - deltaX, ymin - deltaY);

			Head = p1;
			Tail = p2;

			//        long time = System.nanoTime();
			// Sort the points along y-axis
			Points.Sort (_comparator);
			//        logger.info( "Triangulation setup [{}ms]", ( System.nanoTime() - time ) / 1e6 );
		}

		public void FinalizeTriangulation () {
			Triangulatable.AddTriangles (Triangles);
			Triangles.Clear ();
		}

		public override TriangulationConstraint NewConstraint (TriangulationPoint a, TriangulationPoint b) {
			return new DTSweepConstraint (a, b);
		}

	}
}
