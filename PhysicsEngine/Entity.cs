﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Physics;
using RenderInterface;

namespace PhysEngine
{
    class BaseEntity : IEntHandle
    {
        public BaseEntity( FaceMesh[] Meshes, Transform LocalTransform )
        {
            this.Meshes = Meshes;
            this.LocalTransform = LocalTransform;
        }

        public void Close()
        {
            foreach ( FaceMesh m in Meshes )
            {
                m.Close();
            }
        }

        private FaceMesh[] _Meshes;
        public FaceMesh[] Meshes
        {
            get => _Meshes;
            set => _Meshes = value;
        }
        public Transform LocalTransform;

        private BaseEntity _Parent;
        public IEntHandle Parent
        {
            get
            {
                return _Parent;
            }
            set
            {
                Vector AbsPos = GetAbsOrigin();
                Matrix AbsRot = GetAbsRot();
                _Parent = (BaseEntity) value;
                SetAbsOrigin( AbsPos );
                SetAbsRot( AbsRot );
            }
        }

        public void SetLocalOrigin( Vector pt ) => LocalTransform.Position = pt;
        public void SetLocalRot( Matrix r ) => LocalTransform.Rotation = r;
        public void SetLocalScale( Vector s ) => LocalTransform.Scale = s;

        public Vector GetLocalOrigin() => LocalTransform.Position;
        public Matrix GetLocalRot() => LocalTransform.Rotation;
        public Vector GetLocalScale() => LocalTransform.Scale;

        public void SetAbsOrigin( Vector pt )
        {
            if ( Parent != null )
                LocalTransform.Position = Parent.InverseTransformPoint( pt );
            else
                LocalTransform.Position = pt;
        }
        public void SetAbsRot( Matrix r )
        {
            if ( Parent != null )
                LocalTransform.Rotation = -Parent.GetAbsRot() * r;
            else
                LocalTransform.Rotation = r;
        }
        public Vector GetAbsOrigin()
        {
            if ( Parent != null )
                return Parent.TransformPoint( LocalTransform.Position );
            else
                return LocalTransform.Position;
        }
        public Matrix GetAbsRot()
        {
            if ( Parent != null )
                return Parent.GetAbsRot() * LocalTransform.Rotation;
            else
                return LocalTransform.Rotation;
        }


        public Matrix CalcEntMatrix()
        {
            if ( Parent != null )
                return Parent.CalcEntMatrix() * LocalTransform.ThisToWorld;
            return LocalTransform.ThisToWorld;
        }

        public Vector TransformDirection( Vector dir ) => (Vector) ( CalcEntMatrix() * new Vector4( dir, 0.0f ) );
        public Vector TransformPoint( Vector pt ) => (Vector) ( CalcEntMatrix() * new Vector4( pt, 1.0f ) );
        public Vector InverseTransformDirection( Vector dir ) => (Vector) ( -CalcEntMatrix() * new Vector4( dir, 0.0f ) );
        public Vector InverseTransformPoint( Vector pt ) => (Vector) ( -CalcEntMatrix() * new Vector4( pt, 1.0f ) );


        public Plane GetCollisionPlane( Vector pt )
        {
            Plane[] planes = new Plane[ Meshes.Length ];
            for ( int i = 0; i < planes.Length; ++i )
            {
                Vector WorldPoint = TransformPoint( Meshes[ i ].GetVerts()[ 0 ] );
                planes[ i ] = new Plane( TransformDirection( Meshes[ i ].Normal ), Vector.Dot( TransformDirection( Meshes[ i ].Normal ), WorldPoint ) );
            }

            float[] PlaneDists = new float[ planes.Length ];
            for ( int i = 0; i < PlaneDists.Length; ++i )
            {
                PlaneDists[ i ] = Vector.Dot( planes[ i ].Normal, pt ) - planes[ i ].Dist;
            }

            float MaxDist = PlaneDists[ 0 ];
            int MaxIndex = 0;
            for ( int i = 0; i < PlaneDists.Length; ++i )
            {
                if ( PlaneDists[ i ] > MaxDist )
                {
                    MaxIndex = i;
                    MaxDist = PlaneDists[ i ];
                }
            }

            return planes[ MaxIndex ];
        }

        public Vector[] GetVerts()
        {
            HashSet<Vector> Verts = new();
            for ( int i = 0; i < Meshes.Length; ++i )
            {
                Verts.UnionWith( Meshes[ i ].GetVerts() );
            }
            return Verts.ToArray();
        }
        public Vector[] GetWorldVerts()
        {
            Vector[] ret = GetVerts();
            for ( int i = 0; i < ret.Length; ++i )
            {
                ret[ i ] = TransformPoint( ret[ i ] );
            }
            return ret;
        }
        public bool TestCollision( Vector pt )
        {
            Vector[] Points1 = GetWorldVerts();
            Vector[] Points2 = { pt };
            for ( int i = 0; i < Meshes.Length; ++i )
            {
                if ( !Collision.TestCollision( Meshes[ i ].Normal, Points1, Points2 ) )
                    return false;
            }
            return true;
        }
    }

    public enum Space
    {
        NONE      = 0,
        WORLD     = 1 << 0,
        SELF      = 1 << 1,
    }

    class BoxEnt : BaseEntity
    {
        public BoxEnt( Vector mins, Vector maxs, Texture[] tx, bool NormalizeBox = true ) :
            base( new FaceMesh[ 6 ], new Transform( new Vector(), new Vector( 1, 1, 1 ), Matrix.IdentityMatrix() ) )
        {
            if ( NormalizeBox )
            {
                LocalTransform.Position = ( mins + maxs ) / 2;
                mins -= LocalTransform.Position;
                maxs -= LocalTransform.Position;
            }

            AABB = new BBox( mins, maxs );

            int[] inds =
            {
                0, 1, 3,
                1, 2, 3
            };

            float[] ZMins =
            {
                mins.x, mins.y, mins.z, 1.0f, 0.0f,
                maxs.x, mins.y, mins.z, 0.0f, 0.0f,
                maxs.x, maxs.y, mins.z, 0.0f, 1.0f,
                mins.x, maxs.y, mins.z, 1.0f, 1.0f,
            };
            float[] ZMaxs =
            {
                mins.x, mins.y, maxs.z, 0.0f, 0.0f,
                maxs.x, mins.y, maxs.z, 1.0f, 0.0f,
                maxs.x, maxs.y, maxs.z, 1.0f, 1.0f,
                mins.x, maxs.y, maxs.z, 0.0f, 1.0f,
            };
            float[] YMins =
            {
                mins.x, mins.y, mins.z, 0.0f, 0.0f,
                maxs.x, mins.y, mins.z, 1.0f, 0.0f,
                maxs.x, mins.y, maxs.z, 1.0f, 1.0f,
                mins.x, mins.y, maxs.z, 0.0f, 1.0f,
            };
            float[] YMaxs =
            {
                mins.x, maxs.y, mins.z, 1.0f, 0.0f,
                maxs.x, maxs.y, mins.z, 0.0f, 0.0f,
                maxs.x, maxs.y, maxs.z, 0.0f, 1.0f,
                mins.x, maxs.y, maxs.z, 1.0f, 1.0f,
            };
            float[] XMins =
            {
                mins.x, mins.y, mins.z, 0.0f, 0.0f,
                mins.x, maxs.y, mins.z, 0.0f, 1.0f,
                mins.x, maxs.y, maxs.z, 1.0f, 1.0f,
                mins.x, mins.y, maxs.z, 1.0f, 0.0f,
            };
            float[] XMaxs =
            {
                maxs.x, mins.y, mins.z, 1.0f, 0.0f,
                maxs.x, maxs.y, mins.z, 1.0f, 1.0f,
                maxs.x, maxs.y, maxs.z, 0.0f, 1.0f,
                maxs.x, mins.y, maxs.z, 0.0f, 0.0f,
            };

            float[][] Verts = { ZMins, ZMaxs, YMins, YMaxs, XMins, XMaxs };

            Vector[] Normals =
            {
                new Vector( 0, 0,-1 ),
                new Vector( 0, 0, 1 ),
                new Vector( 0,-1, 0 ),
                new Vector( 0, 1, 0 ),
                new Vector(-1, 0, 0 ),
                new Vector( 1, 0, 0 )
            };

            bool SingleTexture = tx.Length == 1;

            for ( int i = 0; i < 6; ++i )
                Meshes[ i ] = new FaceMesh( Verts[ i ], inds, SingleTexture ? tx[ 0 ] : tx[ i ], Normals[ i ] );

        }

        public BBox AABB;
    }

    class Player
    {
        public static readonly Vector EYE_CENTER_OFFSET = new( 0, 0.5f, 0 );
        public static readonly BBox PLAYER_NORMAL_BBOX = new( new Vector( -.5f, -1.0f, -.5f ), new Vector( .5f, 1.0f, .5f ) );
        public static readonly BBox PLAYER_CROUCH_BBOX = new( new Vector( -.5f, -0.5f, -.5f ), new Vector( .5f, 0.5f, .5f ) );
        public static readonly Texture[] BLANK_TEXTURE = { new Texture() };
        //depending on how the compiler works, this may cause a memory leak. Prob won't though
        public static readonly FaceMesh[] PLAYER_NORMAL_FACES = new BoxEnt( PLAYER_NORMAL_BBOX.mins, PLAYER_NORMAL_BBOX.maxs, BLANK_TEXTURE ).Meshes;
        public static readonly FaceMesh[] PLAYER_CROUCH_FACES = new BoxEnt( PLAYER_CROUCH_BBOX.mins, PLAYER_CROUCH_BBOX.maxs, BLANK_TEXTURE ).Meshes;
        public const float PLAYER_MASS = 50.0f;
        public const float PLAYER_ROTI = float.PositiveInfinity;
        public Player( Matrix Perspective, Vector Coeffs, float Mass, float RotI )
        {
            this.Perspective = Perspective;
            _crouched = false;
            Body = new PhysObj( new BoxEnt( PLAYER_NORMAL_BBOX.mins, PLAYER_NORMAL_BBOX.maxs, BLANK_TEXTURE ), Coeffs, Mass, RotI, new() );
            Head = new BaseEntity( Array.Empty<FaceMesh>(), new Transform( new Vector(), new Vector( 1, 1, 1 ), Matrix.IdentityMatrix() ) )
            {
                Parent = (BaseEntity) Body.LinkedEnt
            };
            Head.SetLocalOrigin( EYE_CENTER_OFFSET );
        }
        private bool _crouched;
        public Matrix Perspective;
        public PhysObj Body;
        public BaseEntity Head;
        public BaseEntity HeldEnt;
        public void Crouch()
        {
            if ( !_crouched )
            {
                _crouched = true;
                Body.LinkedEnt.Meshes = PLAYER_CROUCH_FACES;
                Head.SetLocalOrigin( new Vector() );
            }
        }
        public void UnCrouch()
        {
            if ( _crouched )
            {
                _crouched = false;
                Body.LinkedEnt.Meshes = PLAYER_NORMAL_FACES;
                Head.SetLocalOrigin( EYE_CENTER_OFFSET );
            }
        }
    }

    class BBox
    {
        public BBox( Vector mins, Vector maxs )
        {
            this.mins = mins;
            this.maxs = maxs;
        }
        public Vector mins;
        public Vector maxs;

        //member methods
        public bool TestCollisionAABB( BBox bbox, Vector ptThis, Vector ptB2 )
        {
            Vector ptWorldMins1 = this.mins + ptThis;
            Vector ptWorldMaxs1 = this.maxs + ptThis;
            Vector ptWorldMins2 = bbox.mins + ptB2;
            Vector ptWorldMaxs2 = bbox.maxs + ptB2;
            bool bCollisionX = ptWorldMins1.x <= ptWorldMaxs2.x && ptWorldMaxs1.x >= ptWorldMins2.x;
            bool bCollisionY = ptWorldMins1.y <= ptWorldMaxs2.y && ptWorldMaxs1.y >= ptWorldMins2.y;
            bool bCollisionZ = ptWorldMins1.z <= ptWorldMaxs2.z && ptWorldMaxs1.z >= ptWorldMins2.z;
            return bCollisionX && bCollisionY && bCollisionZ;
        }
        public bool TestCollisionPoint( Vector pt, Vector ptThis )
        {
            bool bShouldCollide = true;
            for ( int i = 0; i < 3; ++i )
                if ( !( pt[ i ] > mins[ i ] + ptThis[ i ] && pt[ i ] < maxs[ i ] + ptThis[ i ] ) )
                    bShouldCollide = false;
            return bShouldCollide;
        }
        public Plane GetCollisionPlane( Vector pt, Vector ptB )
        {
            Vector ptWorldMins = mins + ptB;
            Vector ptWorldMaxs = maxs + ptB;

            Plane[] planes =
            {
                new Plane( new Vector( 0, 0, 1 ), Vector.Dot( new Vector( 0, 0, 1 ), ptWorldMaxs ) ),
                new(new Vector(0, 0, -1), Vector.Dot(new Vector(0, 0, -1), ptWorldMins)),
                new Plane( new Vector( 0, 1, 0 ), Vector.Dot( new Vector( 0, 1, 0 ), ptWorldMaxs ) ),
                new Plane( new Vector( 0,-1, 0 ), Vector.Dot( new Vector( 0,-1, 0 ), ptWorldMins ) ),
                new Plane( new Vector( 1, 0, 0 ), Vector.Dot( new Vector( 1, 0, 0 ), ptWorldMaxs ) ),
                new Plane( new Vector( -1, 0, 0 ), Vector.Dot( new Vector( -1, 0, 0 ), ptWorldMins ) ),
            };

            float[] fPlaneDists =
            {
                Vector.Dot( planes[ 0 ].Normal, pt ) - planes[ 0 ].Dist,
                Vector.Dot( planes[ 1 ].Normal, pt ) - planes[ 1 ].Dist,
                Vector.Dot( planes[ 2 ].Normal, pt ) - planes[ 2 ].Dist,
                Vector.Dot( planes[ 3 ].Normal, pt ) - planes[ 3 ].Dist,
                Vector.Dot( planes[ 4 ].Normal, pt ) - planes[ 4 ].Dist,
                Vector.Dot( planes[ 5 ].Normal, pt ) - planes[ 5 ].Dist,
            };

            float fMaxDist = fPlaneDists[ 0 ];
            int iMaxIndex = 0;
            for ( int i = 0; i < 6; ++i )
            {
                if ( fPlaneDists[ i ] > fMaxDist )
                {
                    iMaxIndex = i;
                    fMaxDist = fPlaneDists[ i ];
                }
            }

            return planes[ iMaxIndex ];
        }
        public Vector GetCollisionNormal( Vector pt, Vector ptB )
        {
            Plane p = GetCollisionPlane( pt, ptB );
            return p.Normal;
        }
    }
}
